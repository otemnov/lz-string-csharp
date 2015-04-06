using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace lz_string_csharp
{
    public class LZString
    {
        private class ContextCompress
        {
	        public ContextCompress()
	        {
				Dictionary = new Dictionary<string, int>();
				DictionaryToCreate = new Dictionary<string, bool>();
				c = string.Empty;
		        wc = string.Empty;
				w = string.Empty;
				enlargeIn = 2;
				dictSize = 3;
				numBits = 2;
				Data = new ContextCompressData();
	        }

	        public Dictionary<string, int> Dictionary { get; private set; }
            public Dictionary<string, bool> DictionaryToCreate { get; private set; }
			public ContextCompressData Data { get; set; }
			public string c { get; set; }
			public string wc { get; set; }
			public string w { get; set; }
	        private int enlargeIn { get; set; }
			public int dictSize { get; set; }
			public int numBits { get; set; }

			public void DecrementEnlargeIn()
			{
				enlargeIn--;
				if (enlargeIn == 0)
				{
					enlargeIn = (int)Math.Pow(2, numBits);
					numBits++;
				}
			}
        }

        private class ContextCompressData
        {
	        public ContextCompressData()
	        {
				str = new StringBuilder();
				val = 0;
				position = 0;
	        }

	        public StringBuilder str { get; private set; }
            public int val { get; set; }
            public int position { get; set; }
        }

        private class DecompressData
        {
	        public DecompressData(string compressed, int resetValue, Func<int, int> getNextValue)
			{
				Comptessed = compressed;
				val = getNextValue(0);
				position = resetValue;
				index = 1;
			}

	        public string Comptessed { get; private set; }
            public int val { get; set; }
            public int position { get; set; }
            public int index { get; set; }
        }
		#region Compress
		private static ContextCompressData writeBit(int value, byte bitsPerChar, ContextCompressData data, Func<int, char> getCharFromInt)
        {
            data.val = (data.val << 1) | value;
			if (data.position == bitsPerChar - 1)
            {
                data.position = 0;
				data.str.Append(getCharFromInt(data.val));
                data.val = 0;
            }
            else
                data.position++;

            return data;
        }

		private static ContextCompressData writeBits(int numbits, byte bitsPerChar, int value, ContextCompressData data, Func<int, char> getCharFromInt)
        {

            for (var i = 0; i < numbits; i++)
            {
				data = writeBit(value & 1, bitsPerChar, data, getCharFromInt);
                value = value >> 1;
            }

            return data;
        }

		private static ContextCompress produceW(ContextCompress context, byte bitsPerChar, Func<int, char> getCharFromInt)
        {

            if (context.DictionaryToCreate.ContainsKey(context.w))
            {
                if (context.w[0] < 256)
                {
					context.Data = writeBits(context.numBits, bitsPerChar, 0, context.Data, getCharFromInt);
					context.Data = writeBits(8, bitsPerChar, context.w[0], context.Data, getCharFromInt);
                }
                else
                {
					context.Data = writeBits(context.numBits, bitsPerChar, 1, context.Data, getCharFromInt);
					context.Data = writeBits(16, bitsPerChar, context.w[0], context.Data, getCharFromInt);
                }

				context.DecrementEnlargeIn();
                context.DictionaryToCreate.Remove(context.w);
            }
            else
            {
				context.Data = writeBits(context.numBits, bitsPerChar, context.Dictionary[context.w], context.Data, getCharFromInt);
            }

            return context;
        }

		private static StringBuilder compress(string uncompressed, byte bitsPerChar, Func<int, char> getCharFromInt)
        {
            var context = new ContextCompress();
            for (int i = 0; i < uncompressed.Length; i++)
            {
                context.c = uncompressed[i].ToString(CultureInfo.InvariantCulture);

                if (!context.Dictionary.ContainsKey(context.c))
                {
                    context.Dictionary[context.c] = context.dictSize++;
                    context.DictionaryToCreate[context.c] = true;
                }

                context.wc = context.w + context.c;

                if (context.Dictionary.ContainsKey(context.wc))
                {
                    context.w = context.wc;
                }
                else
                {
					context = produceW(context, bitsPerChar, getCharFromInt);
                    context.DecrementEnlargeIn();
                    context.Dictionary[context.wc] = context.dictSize++;
                    context.w = context.c;
                }
            }

            if (context.w != string.Empty)
            {
				context = produceW(context, bitsPerChar, getCharFromInt);
				context.DecrementEnlargeIn();
            }

			context.Data = writeBits(context.numBits, bitsPerChar, 2, context.Data, getCharFromInt);

			while (true)
			{
				context.Data.val = (context.Data.val << 1);
				if (context.Data.position == bitsPerChar - 1)
				{
					context.Data.str.Append(getCharFromInt(context.Data.val));
					break;
				}
				context.Data.position++;
			}

			return context.Data.str;
        }
		
		public static string CompressToBase64(string input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			const string keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
			StringBuilder res = compress(input, 6, x => keyStr[x]);
			switch (res.Length % 4)
			{
				case 1:
					res.Append("===");
					break;
				case 2:
					res.Append("==");
					break;
				case 3:
					res.Append("=");
					break;
			}
			return res.ToString();
		}

		#endregion
		#region Decompress
		private static int readBit(int resetValue, Func<int, int> getNextValue, DecompressData data)
        {
            var res = data.val & data.position;
            data.position >>= 1;
            if (data.position == 0)
            {
				data.position = resetValue;
				data.val = getNextValue(data.index++); 
            }

            return res > 0 ? 1 : 0;
        }

        private static int readBits(int numBits, int resetValue, Func<int, int> getNextValue, DecompressData data)
        {
            int res = 0;
            var maxpower = (int)Math.Pow(2, numBits);
            int power = 1;
            while (power != maxpower)
            {
				res |= readBit(resetValue, getNextValue, data) * power;
                power <<= 1;
            }
            return res;
        }

		private static string decompress(string compressed, int resetValue, Func<int, int> getNextValue)
        {
			var data = new DecompressData(compressed, resetValue, getNextValue);
            var dictionary = new List<string>();
			int enlargeIn = 4;
            int numBits = 3;
			var result = new StringBuilder();
			string sc = "";

            try
            {
	            for (int i = 0; i < 3; i++)
                {
                    dictionary.Add(i.ToString(CultureInfo.InvariantCulture));
                }

				int next = readBits(2, resetValue, getNextValue, data);

                switch (next)
                {
                    case 0:
						sc = Convert.ToChar(readBits(8, resetValue, getNextValue, data)).ToString(CultureInfo.InvariantCulture);
                        break;
                    case 1:
						sc = Convert.ToChar(readBits(16, resetValue, getNextValue, data)).ToString(CultureInfo.InvariantCulture);
                        break;
                    case 2:
                        return "";
                }

                dictionary.Add(sc);

                result.Append(sc);
				string w = sc;

                while (true)
                {
					int c = readBits(numBits, resetValue, getNextValue, data);
                    int cc = c;

                    switch (cc)
                    {
                        case 0:
							sc = Convert.ToChar(readBits(8, resetValue, getNextValue, data)).ToString();
                            dictionary.Add(sc);
                            c = dictionary.Count - 1;
                            enlargeIn--;

                            break;
                        case 1:
							sc = Convert.ToChar(readBits(16, resetValue, getNextValue, data)).ToString();
                            dictionary.Add(sc);
                            c = dictionary.Count - 1;
                            enlargeIn--;

                            break;
                        case 2:
                            return result.ToString();
                    }

                    if (enlargeIn == 0)
                    {
                        enlargeIn = (int)Math.Pow(2, numBits);
                        numBits++;
                    }

	                string entry;
					if (dictionary.Count - 1 >= c) 
                    {
                        entry = dictionary[c];
                    }
                    else
                    {
                        if (c == dictionary.Count)
                        {
                            entry = w + w[0];
                        }
                        else
                        {
                            return null;
                        }
                    }

                    result.Append(entry);
                    dictionary.Add(w + entry[0]);
                    enlargeIn--;
                    w = entry;

                    if (enlargeIn == 0)
                    {
                        enlargeIn = (int)Math.Pow(2, numBits);
                        numBits++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

		public static string DecompressFromBase64(string input)
        {
	        if (input == null)
	        {
				throw new ArgumentNullException("input");
	        }

            const string keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
			return decompress(input, 32, x => keyStr.IndexOf(input[x]));
		}
		#endregion
	}
}
