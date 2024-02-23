using System.Security.Cryptography;
using System.Text;
using HashLib;

namespace little_heart_bot_3.Crypto;

public static class LiveHeartbeatEncryptor
{
    public static string Encrypt(string data, int[] rules, string key)
    {
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case 0:
                    data = HmacMd5(data, key);
                    data = HmacMd5(data, key);
                    break;
                case 1:
                    data = HmacSha1(data, key);
                    break;
                case 2:
                    data = HmacSha256(data, key);
                    break;
                case 3:
                    data = HmacSha224(data, key);
                    break;
                case 4:
                    data = HmacSha512(data, key);
                    break;
                case 5:
                    data = HmacSha384(data, key);
                    break;
            }
        }

        return data;
    }

    private static string HmacMd5(string data, string key)
    {
        using var hmac = new HMACMD5(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static string HmacSha1(string data, string key)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static string HmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }


    private static string HmacSha512(string data, string key)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static string HmacSha384(string data, string key)
    {
        using var hmac = new HMACSHA384(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }


    private static string HmacSha224(string data, string key)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        IHash hash = HashFactory.Crypto.CreateSHA224();
        var hmac = HashFactory.HMAC.CreateHMAC(hash);
        hmac.Key = keyBytes;
        HashAlgorithm algorithm = HashFactory.Wrappers.HashToHashAlgorithm(hmac);
        byte[] result = algorithm.ComputeHash(dataBytes);
        return BitConverter.ToString(result).Replace("-", "").ToLower();
    }
}