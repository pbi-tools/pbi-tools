// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.Utils
{
    using FileSystem;

    public static class JsonExtensions
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

        /// <summary>
        /// Removes the named property containing an array from the Json object, and returns the array elements as an <see cref="IEnumerable{T}"/>.
        /// Returns an empty enumeration if the property does not exist, but it will fail if the property does not contain an array.
        /// Any array elements that are not of type <see cref="T"/> will be skipped.
        /// </summary>
        public static IEnumerable<T> RemoveArrayAs<T>(this JObject parent, string property)
        {
            var array = parent[property]?.Value<JArray>();
            parent.Remove(property);

            return array != null ? array.OfType<T>() : new T[0];
        }

        /// <summary>
        /// Parses a string-encoded json token from a json object property, removes the property from its parent,
        /// and optionally saves the token in the <see cref="IProjectFolder"/>.
        /// </summary>
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
        /// Adds a new property to the json object, which the string-encoded value of the provided token as the value.
        /// An existing property with the same name will be replaced. 
        /// </summary>
        /// <returns>The original json object, with the new property added.</returns>
        public static JObject InsertTokenAsString<T>(this JObject parent, string property, T token) where T : JToken
        {
            if (token != null)
                parent[property] = token.ToString(formatting: Newtonsoft.Json.Formatting.None);
            return parent;
        }

        /// <summary>
        /// Parses the contents of the file in the specified folder as a Json object and inserts it as a string-encoded
        /// property into the parent object, using the filename (w/o extension) as the property name.
        /// No property is inserted should the file not exist.
        /// An empty Json object is inserted in case of a Json parser error.
        /// </summary>
        public static JObject InsertObjectFromFile(this JObject parent, IProjectFolder folder, string fileName)
        { 
            var objectFile = folder.GetFile(fileName);
            if (objectFile.Exists()) {
                parent.InsertTokenAsString(fileName.WithoutExtension(), objectFile.ReadJson());
            }
            return parent;
        }

        /// <summary>
        /// Parses the contents of the file in the specified folder as a Json array and inserts it as a string-encoded
        /// property into the parent object.
        /// No property is inserted should the file not exist.
        /// An empty Json array is inserted in case of a Json parser error.
        /// </summary>
        public static JObject InsertArrayFromFile(this JObject parent, IProjectFolder folder, string fileName)
        { 
            var arrayFile = folder.GetFile(fileName);
            if (arrayFile.Exists()) {
                parent.InsertTokenAsString(fileName.WithoutExtension(), arrayFile.ReadJsonArray());
            }
            return parent;
        }

        /// <summary>
        /// Saves the <see cref="JToken"/> to the <see cref="IProjectFolder"/> using the <see cref="name"/> provided,
        /// applying all transforms (if any) in specified order.
        /// </summary>
        public static void Save(this JToken token, string name, IProjectFolder folder, params Func<JToken, JToken>[] transforms)
        {
            if (folder == null || token == null) return;
            if (transforms != null) token = transforms.Aggregate(token, (t, func) => func(t));
            folder.Write(token, $"{name}.json");
        }

        public static T ReadPropertySafe<T>(this JObject json, string property, T defaultValue = default(T))
        { 
            if (json == null) throw new ArgumentNullException("json");
            if (json.TryGetValue(property, StringComparison.InvariantCultureIgnoreCase, out var value))
            {
                try
                { 
                    return value.ToObject<T>();
                }
                catch (Exception e)
                {
                    Serilog.Log.Error(e, "Json conversion error occurred reading Property {PropertyName} as Type {TypeName}", property, typeof(T).Name);
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Returns the array with the specified name from the json object. Inserts a new empty array if it doesn't exist.
        /// </summary>
        public static JArray EnsureArray(this JObject parent, string name)
        { 
            var array = parent[name] as JArray;
            if (array == null)
            {
                parent.Add(name, new JArray());
                array = parent[name] as JArray;
            }
            return array;
        }

        /// <summary>
        /// Returns the object with the specified name from the json object. Inserts a new empty object if the property doesn't exist.
        /// </summary>
        public static JObject EnsureObject(this JObject parent, string name)
        {
            var obj = parent[name] as JObject;
            if (obj == null)
            {
                parent.Add(name, new JObject());
                obj = parent[name] as JObject;
            }
            return obj;
        }

        /// <summary>
        /// Adds the specified converters to the <see cref="JsonSerializerSettings"/> instance.
        /// </summary>
        public static JsonSerializerSettings WithConverters(this JsonSerializerSettings settings, params JsonConverter[] converters)
        { 
            if (converters != null && converters.Length > 0)
                Array.ForEach(converters, settings.Converters.Add);
            return settings;
        }

        public static bool TryParseJson<T>(this string json, out T result) where T : class
        {
            try {
                result = JsonConvert.DeserializeObject<T>(json);
                return (result != default);
            }
            catch {
                result = default;
                return false;
            }
        }

        public static bool TryParseJsonObject(this string json, out JObject result)
        {
            try {
                result = JObject.Parse(json);
                return true;
            }
            catch {
                result = default;
                return false;
            }
        }        

        public static bool TryParseJsonArray(this string json, out JArray result)
        {
            try {
                result = JArray.Parse(json);
                return true;
            }
            catch {
                result = default;
                return false;
            }
        }

    }
}