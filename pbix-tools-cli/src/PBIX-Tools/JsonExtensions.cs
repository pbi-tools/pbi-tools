using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PbixTools
{
    public static class ReportJsonExtensions
    {
        /// <summary>
        /// Removes the specified property from its parent, parses its content as a string-encoded <see cref="JObject"/>, and saves the parsed json object
        /// to the provided <see cref="IProjectFolder"/>, using the property name as the filename (unless a null folder is provided).
        /// </summary>
        /// <returns>The <see cref="JObject"/> extracted from the property.</returns>
        public static JObject ExtractObject(this JObject parent, string property, IProjectFolder folder)
        {
            return parent.ExtractToken<JObject>(property, folder);
        }

        /// <summary>
        /// Removes the specified property from its parent, parses its content as a string-encoded <see cref="JArray"/>, and saves the parsed json array
        /// to the provided <see cref="IProjectFolder"/>, using the property name as the filename (unless a null folder is provided).
        /// </summary>
        /// <returns>The <see cref="JArray"/> extracted from the property.</returns>
        public static JArray ExtractArray(this JObject parent, string property, IProjectFolder folder)
        {
            return parent.ExtractToken<JArray>(property, folder);
        }

        public static IEnumerable<T> ExtractArrayAs<T>(this JObject parent, string property)
        {
            var array = parent.ExtractToken<JArray>(property, folder: null);
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
        /// Parses a string-encoded json token out of a property on a json object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <param name="property"></param>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static T ExtractToken<T>(this JObject parent, string property, IProjectFolder folder = null) where T : JToken
        {
            T obj = null;
            var token = parent[property];
            if (token != null)
            {
                parent.Remove(property);
                obj = JToken.Parse(token.Value<string>()) as T;
                obj.Save(property, folder);
            }
            return obj;
        }

        /// <summary>
        /// Saves the <see cref="JToken"/> to the <see cref="IProjectFolder"/> using the <see cref="name"/> provided.
        /// </summary>
        public static void Save(this JToken token, string name, IProjectFolder folder)
        {
            if (folder == null || token == null) return;
            folder.Write(token, $"{name}.json");
        }
    }
}
