using System.Collections.Generic;
using SymbolLibrary.Contracts;

namespace SymbolLibrary.Wpf
{
    public class SymbolCategoryGroup
    {
        public string Category { get; set; }

        public List<SymbolPlacement> Symbols { get; set; }

        public SymbolCategoryGroup(string category)
        {
            Category = category;
            Symbols = new List<SymbolPlacement>();
        }
    }
}