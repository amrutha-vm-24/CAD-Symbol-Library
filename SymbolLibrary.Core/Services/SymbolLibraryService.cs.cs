
using System;
using System.Collections.Generic;
using System.IO;
using SymbolLibrary.Contracts;

namespace SymbolLibrary.Core.Services
{
    public class SymbolLibraryService
    {
        private readonly DxfSymbolReader _reader;

        public SymbolLibraryService()
        {
            _reader = new DxfSymbolReader();
        }

        public List<SymbolPlacement> LoadSymbols(string rootFolder)
        {
            List<SymbolPlacement> symbols = new List<SymbolPlacement>();

            if (string.IsNullOrWhiteSpace(rootFolder) ||
                !Directory.Exists(rootFolder))
            {
                return symbols;
            }

            string[] files = Directory.GetFiles(
                rootFolder,
                "*.dxf",
                SearchOption.AllDirectories);

            foreach (string file in files)
            {
                try
                {
                    if (!_reader.IsValidDxfFile(file))
                        continue;

                    string name =
                        Path.GetFileNameWithoutExtension(file);

                    // Get the actual category folder.
                    string category =
                        new DirectoryInfo(
                            Path.GetDirectoryName(file)!).Name;

                    // Store relative path, e.g. Architecture\Door_Single_900.dxf.
                    string symbolId =
                        Path.GetRelativePath(rootFolder, file);

                    SymbolPlacement placement =
                        _reader.CreateSymbolPlacement(
                            symbolId,
                            name,
                            file);

                    symbols.Add(placement);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Unable to load {file}: {ex.Message}");
                }
            }

            return symbols;
        }
    }
}
