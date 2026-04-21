using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PdfToInp
{
    class ItemIdManager
    {
        private readonly string _filePath;
        private List<ItemEntry> _items;
        private HashSet<int> _usedIds;

        public ItemIdManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        private void Load()
        {
            if (!File.Exists(_filePath))
            {
                _items = new List<ItemEntry>();
                _usedIds = new HashSet<int>();
                return;
            }

            var json = File.ReadAllText(_filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                _items = new List<ItemEntry>();
                _usedIds = new HashSet<int>();
                return;
            }

            try
            {
                var token = JToken.Parse(json);

                if (token.Type == JTokenType.Array)
                {
                    _items = token.ToObject<List<ItemEntry>>() ?? new List<ItemEntry>();
                }
                else if (token.Type == JTokenType.Object)
                {
                    var oldItems = token.ToObject<Dictionary<string, int>>() ?? new Dictionary<string, int>();

                    _items = oldItems
                        .Select(x => new ItemEntry
                        {
                            Name = x.Key,
                            Price = 0m,
                            Id = x.Value
                        })
                        .ToList();

                    Save(); // auto-migrate old format -> new format
                }
                else
                {
                    _items = new List<ItemEntry>();
                }
            }
            catch
            {
                _items = new List<ItemEntry>();
            }

            _usedIds = new HashSet<int>(_items.Select(x => x.Id));
        }

        public int GetId(string itemName, decimal price)
        {
            decimal roundedPrice = Math.Round(price, 2);

            var exactMatch = _items.FirstOrDefault(x =>
                x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) &&
                x.Price == roundedPrice);

            if (exactMatch != null)
            {
                return exactMatch.Id;
            }

            // Fallback for old migrated entries that had no price yet
            var oldEntryMatch = _items.FirstOrDefault(x =>
                x.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) &&
                x.Price == 0m);

            if (oldEntryMatch != null)
            {
                oldEntryMatch.Price = roundedPrice;
                Save();
                return oldEntryMatch.Id;
            }

            int newId;
            var rand = new Random();

            do
            {
                newId = rand.Next(1, 10000);
            }
            while (_usedIds.Contains(newId));

            _items.Add(new ItemEntry
            {
                Name = itemName,
                Price = roundedPrice,
                Id = newId
            });

            _usedIds.Add(newId);
            Save();

            return newId;
        }

        private void Save()
        {
            var json = JsonConvert.SerializeObject(_items, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }

    class ItemEntry
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Id { get; set; }
    }
}