using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PbixTools
{
    public static class JsonExtensions
    {
        public static JObject ExtractObject(this JObject parent, string property, string baseFolder)
        {
            return parent.ExtractToken<JObject>(property, baseFolder);
        }

        public static JArray ExtractArray(this JObject parent, string property, string baseFolder)
        {
            return parent.ExtractToken<JArray>(property, baseFolder);
        }

        public static IEnumerable<T> ExtractArrayAs<T>(this JObject parent, string property)
        {
            var array = parent.ExtractToken<JArray>(property, baseFolder: null);
            if (array != null) return array.OfType<T>();
            else return new T[0];
        }

        public static IEnumerable<T> ArrayAs<T>(this JObject parent, string property)
        {
            var array = parent[property]?.Value<JArray>();
            parent.Remove(property);
            if (array != null) return array.OfType<T>();
            else return new T[0];
        }

        /// <summary>
        /// Parses a string-encoded json token 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <param name="property"></param>
        /// <param name="baseFolder"></param>
        /// <returns></returns>
        public static T ExtractToken<T>(this JObject parent, string property, string baseFolder = null) where T : JToken
        {
            T obj = null;
            var token = parent[property];
            if (token != null)
            {
                parent.Remove(property);
                obj = JToken.Parse(token.Value<string>()) as T;
                obj.Save(property, baseFolder);
            }
            return obj;
        }

        public static void Save(this JToken token, string property, string baseFolder)
        {
            if (baseFolder != null)
                File.WriteAllText(Path.Combine(baseFolder, $"{property}.json"), token.ToString(Newtonsoft.Json.Formatting.Indented));
        }
    }
}
