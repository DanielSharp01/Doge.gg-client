using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Doge.gg_client
{
    public class Config
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        public Config()
        {
            foreach (var line in File.ReadAllLines("config.ini")) {
                var spl = line.Split("=");
                values.Add(spl[0], string.Join("=", spl.Skip(1)));
            }
        }

        public string GetValue(string key)
        {
            return values.ContainsKey(key) ? values[key] : null;
        }
    }
}
