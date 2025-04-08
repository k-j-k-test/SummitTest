using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SummitActuary
{
    public class LTFStream
    {
        public string FilePath { get; set; }
        public string IndexPath { get; set; }
        public Encoding Encoding { get; set; }
        public string Delimiter { get; set; }
        public string LineEnding { get; set; }
        public double Progress { get; set; }
        public bool IsCanceled { get; set; }
        public int BufferSize { get; set; } = 50000;
        public int SkipLines { get; set; }
        public string Extension { get; set; }
        public int[] FixedWidths { get; set; }
        public List<string> DateFormats { get; set; } = new List<string> { "yyyyMMdd", "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd HH:mm:ss" };

        public Dictionary<string, List<long>> Index { get; } = new Dictionary<string, List<long>>();
        public Func<string, string> KeySelector { get; set; }

        public LTFStream(string filePath, Func<string, string> keySelector = null)
        {
            FilePath = filePath;
            KeySelector = keySelector;
            Encoding = Encoding.UTF8;
            Extension = Path.GetExtension(filePath);
            DetectLineEnding();
            DetectDelimiter();
        }

        public void CreateIndex()
        {
            if (KeySelector == null)
                throw new InvalidOperationException("KeySelector is not defined. Index operations require a KeySelector.");
            IndexPath = GetIndexPath();
            Index.Clear();
            string previousKey = null;
            long position = 0;
            long fileSize = new FileInfo(FilePath).Length;
            long counter = 0;

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                // Skip header lines if specified
                for (int i = 0; i < SkipLines; i++)
                {
                    string line = reader.ReadLine();
                    if (line == null) return;
                    position += Encoding.GetByteCount(line) + Encoding.GetByteCount(LineEnding);
                }

                while (!reader.EndOfStream && !IsCanceled)
                {
                    long currentPosition = position;
                    string line = reader.ReadLine();
                    if (line == null) break;

                    position += Encoding.GetByteCount(line) + Encoding.GetByteCount(LineEnding);

                    string currentKey = KeySelector(line);

                    if (previousKey != currentKey)
                    {
                        if (!Index.ContainsKey(currentKey))
                        {
                            Index[currentKey] = new List<long>();
                        }
                        Index[currentKey].Add(currentPosition);
                    }

                    previousKey = currentKey;
                    Progress = (double)position / fileSize;
                    counter++;

                    int currentPercentage = (int)(Progress * 100);
                    if(counter % 10000 == 0)
                    {
                        Console.Write($"\r{FilePath} - Indexing {currentPercentage}% 완료");
                    }

                    if (IsCanceled) break;
                }

                Console.Write($"\r{FilePath} - Indexing 100% 완료");
                Console.WriteLine();
            }

            // Save the index to disk
            using (StreamWriter writer = new StreamWriter(IndexPath, false, Encoding))
            {
                foreach (var pair in Index.OrderBy(x => x.Key))
                {
                    writer.WriteLine($"{pair.Key}|{string.Join(",", pair.Value)}");
                }
            }
        }

        public bool LoadIndex()
        {
            IndexPath = GetIndexPath();

            if (!File.Exists(IndexPath))
            {
                CreateIndex();
            }

            try
            {
                using (StreamReader reader = new StreamReader(IndexPath, Encoding))
                {
                    Index.Clear();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 2)
                        {
                            string key = parts[0];
                            List<long> positions = parts[1]
                                .Split(',')
                                .Select(x => long.Parse(x))
                                .ToList();
                            Index[key] = positions;
                        }
                    }
                    return true;
                }
            }
            catch
            {
                Index.Clear();
                return false;
            }
        }

        public List<string> ReadAll()
        {
            List<string> results = new List<string>();

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                for (int i = 0; i < SkipLines; i++)
                {
                    string line = reader.ReadLine();
                }

                while (!reader.EndOfStream && !IsCanceled)
                {
                    string line = reader.ReadLine();
                    results.Add(line);
                }
            }

            return results;
        }

        public List<T> ReadAll<T>() where T : class, new()
        {
            List<string> stringLines = ReadAll();
            List<T> resultList = new List<T>();

            foreach (string line in stringLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                T instance = new T();

                if (FixedWidths != null && FixedWidths.Length > 0)
                {
                    ConvertFixedWidthDataToObject(line, instance);
                }
                else if (!string.IsNullOrEmpty(Delimiter))
                {
                    ConvertDelimitedDataToObject(line, instance);
                }

                resultList.Add(instance);
            }

            return resultList;
        }

        public List<string> GetLines(string key)
        {
            List<string> results = new List<string>();

            if (!Index.ContainsKey(key))
                return results;

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                foreach (long position in Index[key])
                {
                    if (IsCanceled) break;

                    reader.BaseStream.Position = position;
                    reader.DiscardBufferedData();

                    string line = reader.ReadLine();
                    while (line != null && KeySelector(line) == key)
                    {
                        results.Add(line);
                        line = reader.ReadLine();
                    }
                }
            }

            return results;
        }

        public List<T> GetLines<T>(string key) where T : class, new()
        {
            var stringLines = GetLines(key);
            List<T> resultList = new List<T>();

            if (stringLines.Count == 0)
                return resultList;

            foreach (string line in stringLines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                T instance = new T();

                // Process fixed-width format if FixedWidths is defined
                if (FixedWidths != null && FixedWidths.Length > 0)
                {
                    ConvertFixedWidthDataToObject(line, instance);
                }
                // Otherwise process delimited data if Delimiter is defined
                else if (!string.IsNullOrEmpty(Delimiter))
                {
                    string[] values = line.Split(new[] { Delimiter }, StringSplitOptions.None);
                    ConvertDelimitedDataToObject(line, instance);
                }

                resultList.Add(instance);
            }

            return resultList;
        }

        private List<string> ReadLines(StreamReader reader, ref long position, long fileSize)
        {
            List<string> buffer = new List<string>();

            for (int i = 0; i < BufferSize && !reader.EndOfStream && !IsCanceled; i++)
            {
                string line = reader.ReadLine();
                if (line != null)
                {
                    buffer.Add(line);
                    if (LineEnding != null)
                        position += Encoding.GetByteCount(line) + Encoding.GetByteCount(LineEnding);
                    else
                        position += Encoding.GetByteCount(line);
                }
            }

            // Update progress
            Progress = (double)position / fileSize;

            return buffer;
        }

        public void Write(List<string> lines, bool append = true, string delimiter = "\t")
        {
            string baseDir = CreateProcessingFolder("Written");
            string outputPath = Path.Combine(baseDir, $"output{Extension}");

            using (StreamWriter writer = new StreamWriter(outputPath, append, Encoding))
            {
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }
            }
        }

        public void Write<T>(List<T> items, bool append = true, string delimiter = "\t") where T : class
        {
            string baseDir = CreateProcessingFolder("Written");
            string outputPath = Path.Combine(baseDir, $"output{Extension}");

            using (StreamWriter writer = new StreamWriter(outputPath, append, Encoding))
            {
                foreach (T item in items)
                {
                    string line = ConvertObjectToString(item, delimiter);
                    writer.WriteLine(line);
                }
            }
        }

        public void WriteHeader<T>(bool append = true, string delimiter = "\t") where T : class
        {
            string baseDir = CreateProcessingFolder("Written");
            string outputPath = Path.Combine(baseDir, $"output{Extension}");

            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead)
                .ToArray();

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                sb.Append(property.Name);

                // 마지막 항목이 아니면 구분자 추가
                if (i < properties.Length - 1)
                {
                    sb.Append(delimiter);
                }
            }

            using (StreamWriter writer = new StreamWriter(outputPath, append, Encoding))
            {
                writer.WriteLine(sb.ToString());
            }
        }

        public void Split()
        {
            string baseDir = CreateProcessingFolder("Splited");

            Dictionary<string, StreamWriter> writers = new Dictionary<string, StreamWriter>();
            long fileSize = new FileInfo(FilePath).Length;
            long position = 0;

            try
            {
                using (StreamReader reader = new StreamReader(FilePath, Encoding))
                {
                    // Skip header lines
                    for (int i = 0; i < SkipLines && !reader.EndOfStream; i++)
                        reader.ReadLine();

                    while (!reader.EndOfStream && !IsCanceled)
                    {
                        // Read lines using buffer
                        List<string> buffer = ReadLines(reader, ref position, fileSize);

                        // Process buffer contents by category
                        foreach (string line in buffer)
                        {
                            string category = KeySelector(line);
                            if (string.IsNullOrEmpty(category))
                                continue;

                            // Remove invalid characters from category name
                            foreach (char c in Path.GetInvalidFileNameChars())
                                category = category.Replace(c, '_');

                            if (!writers.ContainsKey(category))
                            {
                                string outputPath = Path.Combine(baseDir, category + Extension);
                                writers[category] = new StreamWriter(outputPath, false, Encoding);
                            }

                            writers[category].WriteLine(line);
                        }
                    }
                }
            }
            finally
            {
                // Close all StreamWriters
                foreach (var writer in writers.Values)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        public Dictionary<string, int> Count()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            long fileSize = new FileInfo(FilePath).Length;
            long position = 0;

            string baseDir = CreateProcessingFolder("Counted");

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                // Skip header lines
                for (int i = 0; i < SkipLines && !reader.EndOfStream; i++)
                    reader.ReadLine();

                while (!reader.EndOfStream && !IsCanceled)
                {
                    // Read lines using buffer
                    List<string> buffer = ReadLines(reader, ref position, fileSize);

                    // Count occurrences
                    foreach (string line in buffer)
                    {
                        string key = KeySelector(line);
                        if (!string.IsNullOrEmpty(key))
                        {
                            if (!counts.ContainsKey(key))
                                counts[key] = 0;
                            counts[key]++;
                        }
                    }
                }
            }

            // Save results to file
            string outputPath = Path.Combine(baseDir, "count_result.txt");
            using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding))
            {
                foreach (var pair in counts.OrderByDescending(x => x.Value))
                {
                    writer.WriteLine($"{pair.Key}|{pair.Value}");
                }
            }

            return counts;
        }

        public List<string> Distinct()
        {
            HashSet<string> keys = new HashSet<string>();
            List<string> distinctLines = new List<string>();
            long fileSize = new FileInfo(FilePath).Length;
            long position = 0;

            string baseDir = CreateProcessingFolder("Distincted");

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                // Skip header lines
                for (int i = 0; i < SkipLines && !reader.EndOfStream; i++)
                    reader.ReadLine();

                while (!reader.EndOfStream && !IsCanceled)
                {
                    // Read lines using buffer
                    List<string> buffer = ReadLines(reader, ref position, fileSize);

                    // Remove duplicates
                    foreach (string line in buffer)
                    {
                        string key = KeySelector(line);
                        if (!string.IsNullOrEmpty(key) && keys.Add(key))
                        {
                            distinctLines.Add(line);
                        }
                    }
                }
            }

            // Save results to file
            string outputPath1 = Path.Combine(baseDir, "distinct_result" + Extension);
            string outputPath2 = Path.Combine(baseDir, "distinct_keys" + Extension);

            using (StreamWriter writer1 = new StreamWriter(outputPath1, false, Encoding))
            using (StreamWriter writer2 = new StreamWriter(outputPath2, false, Encoding))
            {
                foreach (string line in distinctLines)
                {
                    writer1.WriteLine(line);
                }

                foreach (string key in keys)
                {
                    writer2.WriteLine(key);
                }
            }

            return distinctLines;
        }

        public void Sort(Func<string, IComparable> getKey = null)
        {
            List<string> lines = new List<string>();
            long fileSize = new FileInfo(FilePath).Length;
            long position = 0;

            string baseDir = CreateProcessingFolder("Sorted");

            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                // Skip header lines
                for (int i = 0; i < SkipLines && !reader.EndOfStream; i++)
                    reader.ReadLine();

                while (!reader.EndOfStream && !IsCanceled)
                {
                    // Read lines using buffer
                    List<string> buffer = ReadLines(reader, ref position, fileSize);

                    // Store lines
                    lines.AddRange(buffer);
                }
            }

            // Sort the lines
            List<string> sortedLines;
            if (getKey != null)
                sortedLines = lines.OrderBy(getKey).ToList();
            else
                sortedLines = lines.OrderBy(x => x).ToList();

            // Save results to file
            string outputPath = Path.Combine(baseDir, "sorted_result" + Extension);
            using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding))
            {
                foreach (string line in sortedLines)
                {
                    writer.WriteLine(line);
                }
            }
        }

        public Dictionary<string, List<List<string>>> Match(params LTFStream[] otherReaders)
        {
            if (otherReaders == null)
                throw new ArgumentException("At least one other LTFReader must be provided", nameof(otherReaders));

            var result = new Dictionary<string, List<List<string>>>();

            var thisReaderData = new Dictionary<string, List<string>>();

            using (StreamReader streamReader = new StreamReader(this.FilePath, this.Encoding))
            {
                for (int i = 0; i < this.SkipLines && !streamReader.EndOfStream; i++)
                    streamReader.ReadLine();

                while (!streamReader.EndOfStream)
                {
                    string line = streamReader.ReadLine();
                    string key = this.KeySelector(line);

                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!thisReaderData.ContainsKey(key))
                        thisReaderData[key] = new List<string>();

                    thisReaderData[key].Add(line);
                }
            }

            foreach (var pair in thisReaderData)
            {
                string key = pair.Key;
                List<string> lines = pair.Value;

                var resultLists = new List<List<string>>();
                resultLists.Add(lines);

                for (int i = 0; i < otherReaders.Length; i++)
                {
                    resultLists.Add(new List<string>());
                }

                result[key] = resultLists;
            }

            for (int readerIndex = 0; readerIndex < otherReaders.Length; readerIndex++)
            {
                var reader = otherReaders[readerIndex];
                if (reader == null) continue;

                using (StreamReader streamReader = new StreamReader(reader.FilePath, reader.Encoding))
                {
                    for (int i = 0; i < reader.SkipLines && !streamReader.EndOfStream; i++)
                        streamReader.ReadLine();

                    while (!streamReader.EndOfStream)
                    {
                        string line = streamReader.ReadLine();
                        string key = reader.KeySelector(line);

                        if (string.IsNullOrEmpty(key) || !result.ContainsKey(key))
                            continue;

                        result[key][readerIndex + 1].Add(line);
                    }
                }
            }

            return result;
        }

        public List<string> Sample(string key)
        {
            if (string.IsNullOrEmpty(key) || !Index.ContainsKey(key))
                return new List<string>();

            List<string> lines = GetLines(key);

            string baseDir = CreateProcessingFolder("Sampled");
            string outputPath = Path.Combine(baseDir, $"sample_{key}{Extension}");
            List<string> result = new List<string>();

            using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding))
            {
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                    result.Add(line);
                }
            }

            return result;
        }

        public object ChangeType(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value) && targetType.IsValueType)
            {
                return Activator.CreateInstance(targetType);
            }

            try
            {
                if (targetType == typeof(DateTime))
                {
                    if (DateTime.TryParseExact(value, DateFormats.ToArray(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateResult))
                    {
                        return dateResult;
                    }

                    return DateTime.MinValue;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return Activator.CreateInstance(targetType);
            }
        }

        public void ConvertDelimitedDataToObject<T>(string line, T instance) where T : class
        {
            string[] values = line.Split(new[] { Delimiter }, StringSplitOptions.None);

            var properties = typeof(T).GetProperties()
                .Where(p => p.CanWrite)
                .Take(values.Length)
                .ToArray();

            for (int i = 0; i < values.Length && i < properties.Length; i++)
            {
                try
                {
                    var property = properties[i];

                    object convertedValue = ChangeType(values[i].Trim(), property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch
                {
                    // Keep default value if conversion fails
                }
            }
        }

        public void ConvertFixedWidthDataToObject<T>(string line, T instance) where T : class
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanWrite)
                .Take(FixedWidths.Length)
                .ToArray();

            int position = 0;

            for (int i = 0; i < FixedWidths.Length && i < properties.Length; i++)
            {
                int width = FixedWidths[i];

                // Make sure we don't go past the end of the line
                if (position >= line.Length)
                    break;

                // Calculate the actual width to extract
                int actualWidth = (position + width <= line.Length)
                    ? width
                    : line.Length - position;

                if (actualWidth <= 0)
                    continue;

                string value = line.Substring(position, actualWidth).Trim();
                position += width;

                try
                {
                    var property = properties[i];
                    object convertedValue = ChangeType(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch
                {
                    // Keep default value if conversion fails
                }
            }
        }

        public string ConvertObjectToString<T>(T obj, string delimiter) where T : class
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead)
                .ToArray();

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                object value = property.GetValue(obj);

                if (value != null)
                {
                    // 날짜 형식 처리
                    if (value is DateTime dateValue && dateValue != DateTime.MinValue)
                    {
                        sb.Append(dateValue.ToString(DateFormats.FirstOrDefault() ?? "yyyyMMdd"));
                    }
                    else
                    {
                        sb.Append(value.ToString());
                    }
                }

                // 마지막 항목이 아니면 구분자 추가
                if (i < properties.Length - 1)
                {
                    sb.Append(delimiter);
                }
            }

            return sb.ToString();
        }

        private void DetectDelimiter()
        {
            var commonDelimiters = new[] { "\t", ",", "|", ";", "^" };
            var delimiterCounts = new Dictionary<string, int>();

            using (var reader = new StreamReader(FilePath, Encoding))
            {
                // Check first 10 lines (file may be shorter)
                for (int i = 0; i < 10 && !reader.EndOfStream; i++)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue;

                    foreach (var delimiter in commonDelimiters)
                    {
                        // Increase count if splitting by delimiter produces more than 1 field
                        if (line.Split(new[] { delimiter }, StringSplitOptions.None).Length > 1)
                        {
                            if (!delimiterCounts.ContainsKey(delimiter))
                                delimiterCounts[delimiter] = 0;
                            delimiterCounts[delimiter]++;
                        }
                    }
                }
            }

            // Select the most frequently found delimiter
            if (delimiterCounts.Any())
            {
                Delimiter = delimiterCounts.OrderByDescending(x => x.Value).First().Key;
            }
            else
            {
                // Set default
                Delimiter = null;
            }
        }

        private void DetectLineEnding()
        {
            using (StreamReader reader = new StreamReader(FilePath, Encoding))
            {
                char[] buffer = new char[4096];
                int read = reader.Read(buffer, 0, buffer.Length);

                if (read > 0)
                {
                    string text = new string(buffer, 0, read);
                    LineEnding = text.Contains("\r\n") ? "\r\n" :
                                text.Contains("\n") ? "\n" :
                                "\r\n";  // Windows default
                }
                else
                {
                    LineEnding = "\r\n";  // Empty file, use Windows default
                }
            }
        }

        public string CreateProcessingFolder(string operationName)
        {
            string directoryPath = Path.GetDirectoryName(FilePath);
            string fileName = Path.GetFileNameWithoutExtension(FilePath);

            // Create operation folder (e.g., 'Splited', 'Sorted', etc.)
            string operationDir = Path.Combine(directoryPath, operationName);
            if (!Directory.Exists(operationDir))
                Directory.CreateDirectory(operationDir);

            // Create filename folder
            string baseDir = Path.Combine(operationDir, fileName);
            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            return baseDir;
        }

        private string GetIndexPath()
        {
            string fileName = Path.GetFileNameWithoutExtension(FilePath);
            string directoryPath = Path.GetDirectoryName(FilePath);

            // Create folder structure similar to other processors
            string indexDir = Path.Combine(directoryPath, "Index");
            if (!Directory.Exists(indexDir))
                Directory.CreateDirectory(indexDir);

            string fileDir = Path.Combine(indexDir, fileName);
            if (!Directory.Exists(fileDir))
                Directory.CreateDirectory(fileDir);

            return Path.Combine(fileDir, $"{fileName}.idx");
        }
    }
}