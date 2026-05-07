using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RestaurantAPI.Services
{
    public class VnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, string returnUrl, string ipAddr)
        {
            var tmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE") ?? _config["VnPay:TmnCode"] ?? "LQ1203S3";
            var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET") ?? _config["VnPay:HashSecret"] ?? "30F753TNRCBSBBHZ8XUWT05V3UP84VSN";
            var vnpUrl = Environment.GetEnvironmentVariable("VNPAY_URL") ?? _config["VnPay:Url"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

            var now = DateTime.UtcNow.AddHours(7); // Vietnam time
            var txnRef = $"{orderId}_{now:yyyyMMddHHmmss}";

            var vnpParams = new SortedDictionary<string, string>
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode,
                ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = txnRef,
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = "vn",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_IpAddr"] = ipAddr,
                ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
                ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
            };

            var query = string.Join("&", vnpParams.Select(kv =>
                $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

            var secureHash = HmacSha512(hashSecret, query);

            return $"{vnpUrl}?{query}&vnp_SecureHash={secureHash}";
        }

        public bool ValidateSignature(IQueryCollection query, out string txnRef, out string responseCode, out long amount)
        {
            txnRef = query["vnp_TxnRef"].ToString();
            responseCode = query["vnp_ResponseCode"].ToString();
            long.TryParse(query["vnp_Amount"].ToString(), out amount);

            var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET")
                ?? _config["VnPay:HashSecret"] ?? "30F753TNRCBSBBHZ8XUWT05V3UP84VSN";

            var secureHash = query["vnp_SecureHash"].ToString();

            var sortedParams = new SortedDictionary<string, string>();
            foreach (var key in query.Keys)
            {
                if (key.StartsWith("vnp_") && key != "vnp_SecureHash" && key != "vnp_SecureHashType")
                    sortedParams[key] = query[key].ToString();
            }

            var signData = string.Join("&", sortedParams.Select(kv =>
                $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

            var expectedHash = HmacSha512(hashSecret, signData);

            return string.Equals(expectedHash, secureHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}