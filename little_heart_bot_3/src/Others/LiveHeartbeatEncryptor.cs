using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace little_heart_bot_3.Others;

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
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
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

        HMac hmac = new HMac(new Sha224Digest());
        hmac.Init(new KeyParameter(keyBytes));
        hmac.BlockUpdate(dataBytes, 0, dataBytes.Length);

        byte[] result = new byte[hmac.GetMacSize()];
        hmac.DoFinal(result, 0);

        return BitConverter.ToString(result).Replace("-", "").ToLower();
    }
}