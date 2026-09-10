using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace SkolasCelvedis.JsonHandle
{
    public class JsonRepository<T>
    {
        private readonly string _filePath;

        public JsonRepository(string filePath)
        {
            _filePath = filePath;
            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "[]");
        }

        public List<T> GetAll()
        {
            string json = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        public void SaveAll(List<T> records)
        {
            string json = JsonConvert.SerializeObject(records, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public void Add(T record)
        {
            var all = GetAll();
            all.Add(record);
            SaveAll(all);
        }

       
    }
}
