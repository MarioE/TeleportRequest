using System;
using System.IO;
using Newtonsoft.Json;

namespace TeleportRequest
{
	public class Config
	{
		public int Interval = 3;
		public int Timeout = 3;

		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				Write(fs);
			}
		}
		public void Write(Stream stream)
		{
			var str = JsonConvert.SerializeObject(this, Formatting.Indented);
			using (var sw = new StreamWriter(stream))
			{
				sw.Write(str);
			}
		}

		public static Config Read(string path)
		{
			if (!File.Exists(path))
			{
				return new Config();
			}
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Read(fs);
			}
		}
		public static Config Read(Stream stream)
		{
			using (var sr = new StreamReader(stream))
			{
				return JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
			}
		}
	}
}
