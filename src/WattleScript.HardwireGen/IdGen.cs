using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WattleScript.HardwireGen 
{
    static class IdGen
    {
        private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
        private static Dictionary<string, string> generated = new Dictionary<string, string>();
        public static void Reset() => generated = new Dictionary<string, string>();
        public static string Create(string str)
        {
            long hash = FNV1A_56(str);
            var id = Encode(hash);
            while(generated.TryGetValue(id, out var str2) && str2 != str)
            {
                hash++;
                id = Encode(hash);
            }
            generated[id] = str;
            return id;
        }
        
        static long FNV1A_56(string str)
        {
            const ulong fnv64Offset = 14695981039346656037;
            const ulong fnv64Prime = 0x100000001b3;
            ulong hash = fnv64Offset;
            for (var i = 0; i < str.Length; i++)
            {
                hash ^= str[i];
                hash *= fnv64Prime;
            }
            // Reduce to 56-bit hash.
            const ulong MASK_56 = (1UL << 56) - 1;
            hash = (hash >> 56) ^ (hash & MASK_56);
            return unchecked((long) hash);
        }
        
        static string Encode(long number)
        {
            if (number < 0) {
                throw new ArgumentException("number < 0");
            }
            var builder = new StringBuilder();
            var divisor = ALPHABET.Length;
            while (number > 0)
            {
                number = Math.DivRem(number, divisor, out var rem);
                builder.Append(ALPHABET[(int) rem]);
            }
            return new string(builder.ToString().Reverse().ToArray());
        }
    }
}