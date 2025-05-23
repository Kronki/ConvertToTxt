using iTextSharp.text.pdf.parser;
using PdfToInp;
using iTextSharp.text.pdf;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml;

bool exitRequested = false;

// Get the directory of the .sln file (assuming the .sln is in the current working directory)
string solutionDirectory = AppContext.BaseDirectory;

// Call the function to print
Console.WriteLine("HOXXES permes keti aplikacioni ju mundeson printimin e kuponave fiscal");
Console.WriteLine("\nJu lutem mbani te hapur kete program qe te ju funksionoj printimi i kuponave fiskal!");

while (!exitRequested)
{
    string directoryPath = solutionDirectory;
    string outputPath = System.IO.Path.Combine(solutionDirectory, "FILE_IN");

    // Start monitoring the directory
    MonitorDirectory(directoryPath, outputPath);

    // Sleep for 2 seconds before checking again
    Thread.Sleep(2000);
}

// Monitoring directory logic
static void MonitorDirectory(string directoryPath, string outputPath)
{
    var itemIdManager = new ItemIdManager(System.IO.Path.Combine(directoryPath, "item_ids.json"));

    var pdfFiles = Directory.GetFiles(directoryPath, "*.pdf");

    foreach (var pdfFile in pdfFiles)
    {
        try
        {
            // Process the PDF file and extract order items
            (List<OrderItem> orderItems, int pages, int orderNr) = ExtractOrderItemsFromPdf(pdfFile);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(pdfFile);
            string uniqueFileName = $"{fileName}_{Guid.NewGuid()}.inp";
            string inpFilePath = System.IO.Path.Combine(outputPath, uniqueFileName);
            //SaveOrderItemsToFile(inpFilePath, orderItems, itemIdManager);

            string xmlFileName = $"{fileName}_{Guid.NewGuid()}.xml";
            string xmlFilePath = System.IO.Path.Combine(outputPath, xmlFileName);
            BuildOrderXml(orderItems, orderNr, xmlFilePath);

            // Delete the original PDF file after processing
            File.Delete(pdfFile);
        }
        catch (Exception ex)
        {
            // Log the exception to a file, grouped by date
            string logFilePath = System.IO.Path.Combine(outputPath, $"ErrorLog_{DateTime.Now:yyyy-MM-dd}.txt");

            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing {pdfFile}: {ex.Message}{Environment.NewLine}";

            // Append the error message to the log file
            File.AppendAllText(logFilePath, logMessage);
        }
    }
}


// Method to extract order items from a PDF file
static (List<OrderItem>, int, int) ExtractOrderItemsFromPdf(string pdfFilePath)
{
    List<OrderItem> orderItems = new List<OrderItem>();
    var numberOfPages = 0;
    int operNum = 1; // Default value
    using (PdfReader reader = new PdfReader(pdfFilePath))
    {
        StringWriter output = new StringWriter();
        if (reader.NumberOfPages > 10)
        {
            numberOfPages = reader.NumberOfPages;
            return (new(), reader.NumberOfPages, 0);
        }


        for (int i = 1; i <= reader.NumberOfPages; i++)
        {
            string pageText = PdfTextExtractor.GetTextFromPage(reader, i);
            string[] lines = pageText.Split('\n');

            foreach (string line in lines)
            {
                var operMatch = Regex.Match(line, @"#(?<OperNum>\d+)");
                if (operMatch.Success && int.TryParse(operMatch.Groups["OperNum"].Value, out int parsedOperNum))
                {
                    operNum = parsedOperNum;
                }

                var match = Regex.Match(line, @"^(?<Name>.+?)\s+(?<Quantity>\d+)\s+€?\s*(?<Price>\d+(\.\d{1,2})?)$");

                if (match.Success)
                {
                    string name = match.Groups["Name"].Value.Trim();
                    int quantity = int.Parse(match.Groups["Quantity"].Value);
                    decimal price = decimal.Parse(match.Groups["Price"].Value);

                    orderItems.Add(new OrderItem
                    {
                        Name = name,
                        Quantity = quantity,
                        Price = price
                    });
                }
            }
        }
    }

    return (orderItems, numberOfPages, operNum);
}

// Method to save order items to a file
static void SaveOrderItemsToFile(string filePath, List<OrderItem> orderItems, ItemIdManager itemIdManager)
{
    var rand = new Random();
    // Ensure the file extension is .inp
    if (System.IO.Path.GetExtension(filePath).ToLower() != ".inp")
    {
        filePath = System.IO.Path.ChangeExtension(filePath, ".inp");
    }

    // Prepare the content as a string
    var content = new StringBuilder();
    foreach (var item in orderItems)
    {
        var itemId = itemIdManager.GetId(item.Name);
        var priceDivided = item.Price / item.Quantity;
        var price = Convert.ToDouble(String.Format("{0:0.00}", priceDivided));
        content.AppendLine($"S,1,______,_,__;{item.Name};{price};{item.Quantity};1;1;5;0;{itemId};0;0;");
    }
    if (orderItems.Count > 0)
    {
        content.AppendLine("T,1,______,_,__;0");

        // Write the content to the file
        File.WriteAllText(filePath, content.ToString());
    }
}
static void BuildOrderXml(List<OrderItem> orderItems, int orderNr, string filePath)
{
    var commands = new List<XElement>();

    // OpenReceipt command
    commands.Add(
        new XElement("Command", new XAttribute("Name", "OpenReceipt"),
            new XElement("Args",
                new XElement("Arg", new XAttribute("Name", "OperNum"), new XAttribute("Value", orderNr)),
                new XElement("Arg", new XAttribute("Name", "OperPass"), new XAttribute("Value", "0")),
                new XElement("Arg", new XAttribute("Name", "OptionPrintType"), new XAttribute("Value", "0"))
            )
        )
    );

    // Order items
    foreach (var item in orderItems)
    {
        commands.Add(
            new XElement("Command", new XAttribute("Name", "SellPLUwithSpecifiedVAT"),
                new XElement("Args",
                    new XElement("Arg", new XAttribute("Name", "NamePLU"), new XAttribute("Value", item.Name)),
                    new XElement("Arg", new XAttribute("Name", "OptionVATClass"), new XAttribute("Value", "C")),
                    new XElement("Arg", new XAttribute("Name", "Price"), new XAttribute("Value", item.Price)),
                    new XElement("Arg", new XAttribute("Name", "Quantity"), new XAttribute("Value", item.Quantity)),
                    new XElement("Arg", new XAttribute("Name", "DiscAddP"), new XAttribute("Value", "-0")),
                    new XElement("Arg", new XAttribute("Name", "DiscAddV"), new XAttribute("Value", "0")),
                    new XElement("Arg", new XAttribute("Name", "DepNum"), new XAttribute("Value", "0"))
                )
            )
        );
    }

    // Subtotal
    commands.Add(
        new XElement("Command", new XAttribute("Name", "Subtotal"),
            new XElement("Args",
                new XElement("Arg", new XAttribute("Name", "OptionPrinting"), new XAttribute("Value", "1")),
                new XElement("Arg", new XAttribute("Name", "OptionDisplay"), new XAttribute("Value", "0")),
                new XElement("Arg", new XAttribute("Name", "DiscAddV"), new XAttribute("Value", "0")),
                new XElement("Arg", new XAttribute("Name", "DiscAddP"), new XAttribute("Value", "0"))
            )
        )
    );

    // CashPayCloseReceipt
    commands.Add(new XElement("Command", new XAttribute("Name", "CashPayCloseReceipt")));

    SaveCommandsToXmlFile(commands, filePath);
}

static void SaveCommandsToXmlFile(List<XElement> commands, string filePath)
{
    var settings = new XmlWriterSettings
    {
        Indent = true,
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Fragment
    };

    using (var writer = XmlWriter.Create(filePath, settings))
    {
        foreach (var command in commands)
        {
            command.WriteTo(writer);
        }
    }
}