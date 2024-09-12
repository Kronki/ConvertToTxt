using Newtonsoft.Json;

namespace PdfToInp
{
    class ItemIdManager
    {
        private readonly string _filePath;
        private Dictionary<string, int> _items;
        private HashSet<int> _usedIds; // Track used IDs to ensure uniqueness

        public ItemIdManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        private void Load()
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _items = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                _usedIds = new HashSet<int>(_items.Values); // Initialize with existing IDs
            }
            else
            {
                _items = new Dictionary<string, int>();
                _usedIds = new HashSet<int>();
            }
        }

        public int GetId(string itemName)
        {
            if (_items.TryGetValue(itemName, out var id))
            {
                return id;
            }

            // Generate a unique ID that hasn't been used yet
            int newId;
            var rand = new Random();
            do
            {
                newId = rand.Next(1, 10000); // Adjust the range as needed
            }
            while (_usedIds.Contains(newId));

            _items[itemName] = newId;
            _usedIds.Add(newId); // Mark this ID as used
            Save();
            return newId;
        }

        private void Save()
        {
            var json = JsonConvert.SerializeObject(_items, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}
