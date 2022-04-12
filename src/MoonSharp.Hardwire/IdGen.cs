using System;
using System.Linq;
using System.Reflection;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;

namespace MoonSharp.Hardwire
{
    static class IdGen
    {
        private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
        public static string Create(string str)
        {
            using (MD5 md5Hash = MD5.Create())  
            {  
                var bLen = BitConverter.GetBytes((ushort) str.Length);
                byte[] bytes = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(str));
                return Encode(new BigInteger(bLen.Concat(bytes).Append((byte)0).ToArray()));
            }
        }
        static string Encode(BigInteger number)
        {
            if(number < 0)
                throw new ArgumentException();
            var builder = new StringBuilder();
            var divisor = new BigInteger(ALPHABET.Length);
            while (number > 0)
            {
                number = BigInteger.DivRem(number, divisor, out var rem);
                builder.Append(ALPHABET[(int) rem]);
            }
            return new string(builder.ToString().Reverse().ToArray());
        }
    }
}