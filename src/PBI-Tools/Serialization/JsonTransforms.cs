using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbiTools.Serialization
{
    public static class JsonTransforms
    {
        /// <summary>
        /// Sorts the properties of all nested Json objects alphabetically.
        /// </summary>
        public static JToken SortProperties(this JToken token)
        {
            switch (token)
            {
                case JObject obj:
                    return new JObject(obj.Properties().OrderBy(x => x.Name).Select(x => new JProperty(x.Name, x.Value.SortProperties())));
                case JArray arr:
                    return new JArray(arr.Select(SortProperties));
                default:
                    return token;
            }
        }


        public static JToken NormalizeNumbers(this JToken token)
        {
            if (token is JObject obj)
                return new JObject(obj.Properties().Select(x => new JProperty(x.Name, x.Value.NormalizeNumbers())));
            else if (token.Type == JTokenType.Float && Math.Abs(token.Value<float>() - token.Value<int>()) <= 0.0001)
                return token.Value<int>();
            else return token;
        }

        /// <summary>
        /// Removes all named properties from the provided Json object (root level only).
        /// </summary>
        public static Func<JToken, JToken> RemoveProperties(params string[] propertyNames)
        {
            return token =>
            {
                if (token is JObject obj && propertyNames != null)
                {
                    Array.ForEach(propertyNames, propertyName => 
                    {
                        if (obj.ContainsKey(propertyName))
                            obj.Remove(propertyName);
                    });
                }

                return token;
            };
        }


        /// <summary>
        /// Recursively removes all named properties from the provided Json object.
        /// </summary>
        public static JObject RemoveProperties(this JObject json, params string[] propertyNames)
        {
            JToken RemovePropertiesRec(JToken token)
            {
                switch (token)
                {
                    case JObject obj:
                        return propertyNames == null ? obj : new JObject(obj.Properties()
                            .Where(p => !propertyNames.Contains(p.Name))
                            .Select(p => new JProperty(p.Name, RemovePropertiesRec(p.Value))));
                    case JArray arr:
                        return new JArray(arr.Select(RemovePropertiesRec));
                    default:
                        return token;
                }
            };

            return RemovePropertiesRec(json) as JObject;
        }
    }
}
