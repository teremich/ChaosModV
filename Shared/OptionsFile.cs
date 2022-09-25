using System;
using System.Collections.Generic;
using System.IO;

namespace Shared
{
    public class OptionsFile
    {
        private string m_fileName;
        private Dictionary<string, string> m_options = new Dictionary<string, string>();

        public OptionsFile(string fileName)
        {
            m_fileName = fileName;
            ReadFile();
        }

        private static T Require<T>(string key, string fileName)
        {
            throw new KeyNotFound(key, fileName);
        }

        public bool HasKey(string key)
        {
            return m_options.ContainsKey(key);
        }

        public IEnumerable<string> GetKeys()
        {
            foreach (var option in m_options)
            {
                yield return option.Key;
            }
        }

        /// <summary>
        /// Similar to <see cref="ReadValueBool" />, but requires the
        /// bool to be present in the file.
        /// Note that booleans are represented using the digits '0' and '1'.
        /// </summary>
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="FormatException" />
        /// <exception cref="OverflowException" />
        public bool RequireBool(string key)
        {
            if (!HasKey(key))
            {
                return Require<bool>(key, m_fileName);
            }

            try
            {
                if (int.Parse(ReadValue(key)) == 0)
                {
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                throw new FailedToParse(key, key, m_fileName, e);
            }
        }

        /// <summary>
        /// Requires the key to result in a value that is a valid variant of the
        /// specified enum.
        /// </summary>
        /// <exception cref="KeyNotFound" />
        /// <exception cref="InvalidEnumVariant" />
        public T RequireEnum<T>(string key) where T : struct, Enum
        {
            var value = RequireString(key);

            try
            {
                return Enum.Parse<T>(value);
            }
            catch (ArgumentException)
            {
                throw new InvalidEnumVariant<T>(value, key);
            }
        }

        /// <summary>
        /// Similar to <see cref="ReadValueInt" />, but requires the
        /// int to be present in the file.
        /// </summary>
        /// <exception cref="KeyNotFound" />
        /// <exception cref="ArgumentNullException" />
        /// <exception cref="FormatException" />
        /// <exception cref="OverflowException" />
        public int RequireInt(string key)
        {
            if (!HasKey(key))
            {
                return Require<int>(key, m_fileName);
            }

            try
            {
                return int.Parse(ReadValue(key));
            }
            catch (Exception e)
            {
                throw new FailedToParse(key, key, m_fileName, e);
            }
        }

        /// <summary>
        /// Similar to <see cref="ReadValue" />, but requires the
        /// value to be present in the file.
        /// </summary>
        /// <exception cref="KeyNotFound" />
        public string RequireString(string key)
        {
            return ReadValue(key) ?? Require<string>(key, m_fileName);
        }

        // HACK: Remove pragma warning disable
        // This is only added because the file is compiled in two different projects.
        // One of them checks for strict nullability (voting proxy), the other doesn't (config app).
        // In addition, the config app uses an other framework target, so the optional operator (?)
        // cannot be used.
        // The solution is to upgrade the config app to a newer framework target.
#pragma warning disable CS8625
        public string ReadValue(string key, string defaultValue = null)
#pragma warning restore CS8625
        {
            return HasKey(key) ? m_options[key] : defaultValue;
        }

        public int ReadValueInt(string key, int defaultValue)
        {
            int result;
            if (!int.TryParse(ReadValue(key), out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public bool ReadValueBool(string key, bool defaultValue)
        {
            bool result = defaultValue;
            if (HasKey(key))
            {
                if (int.TryParse(ReadValue(key), out int tmp))
                {
                    result = tmp != 0;
                }
            }

            return result;
        }

        public void WriteValue(string key, string value)
        {
            if (value != null && value.Trim().Length > 0)
            {
                m_options[key] = value;
            }
            else
            {
                m_options.Remove(key);
            }
        }

        public void WriteValue(string key, int value)
        {
            WriteValue(key, $"{value}");
        }

        public void WriteValue(string key, bool value)
        {
            WriteValue(key, value ? 1 : 0);
        }

        public void ReadFile()
        {
            if (!File.Exists(m_fileName))
            {
                return;
            }

            string data = File.ReadAllText(m_fileName);
            if (data.Length == 0)
            {
                return;
            }

            m_options.Clear();

            foreach (string line in data.Split('\n'))
            {
                string[] keyValuePair = line.Split('=');
                if (keyValuePair.Length != 2)
                {
                    continue;
                }

                m_options[keyValuePair[0]] = keyValuePair[1];
            }
        }

        public void WriteFile()
        {
            string data = string.Empty;

            foreach (var option in m_options)
            {
                data += $"{option.Key}={option.Value}\n";
            }

            File.WriteAllText(m_fileName, data);
        }

        public void ResetFile()
        {
            File.WriteAllText(m_fileName, null);

            // Reset all options too
            m_options = new Dictionary<string, string>();
        }


        public class KeyNotFound : Exception
        {
            public KeyNotFound(string key, string fileName)
                : base($"could not find the key '{key}' in the options file '{fileName}'")
            { }
        }

        public class FailedToParse : Exception
        {
            public FailedToParse(
                string value,
                string key,
                string fileName,
                Exception originalException
            ) : base($"could not parse the value '{value}', key: '{key}', file: {fileName}", originalException)
            { }
        }

        public class InvalidEnumVariant<T> : Exception where T : struct, Enum
        {

            public InvalidEnumVariant(string value, string key) : base(GetMessage(value, key))
            { }

            private static string GetMessage(string value, string key)
            {
                var allowedVariants = String.Join(", ", Enum.GetNames<T>());

                return $"the value '{value}' is not a valid variant for '{key}', "
                    + $"allowed values are: [{allowedVariants}]";
            }

        }
    }
}
