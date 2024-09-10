using iTextSharp.text.pdf.parser;
using PdfToInp;
using iTextSharp.text.pdf;
using System.Text;
using System.Text.RegularExpressions;

bool exitRequested = false;

// Get the directory of the .sln file (assuming the .sln is in the current working directory)
string solutionDirectory = AppContext.BaseDirectory;

// Call the function to print
Console.WriteLine("HOXXES permes keti aplikacioni ju mundeson printimin e kuponave fiscal");
Console.WriteLine("\nJu lutem mbani te hapur kete program qe te ju funksionoj printimi i kuponave fiskal!");

while (!exitRequested)
{
    string directoryPath = solutionDirectory;
    string outputPath = solutionDirectory;

    // Start monitoring the directory
    MonitorDirectory(directoryPath, outputPath);

    // Sleep for 2 seconds before checking again
    Thread.Sleep(2000);
}

// Monitoring directory logic
static void MonitorDirectory(string directoryPath, string outputPath)
{
    var pdfFiles = Directory.GetFiles(directoryPath, "*.pdf");

    foreach (var pdfFile in pdfFiles)
    {
        try
        {
            // Process the PDF file and extract order items
            (List<OrderItem> orderItems, int pages) = ExtractOrderItemsFromPdf(pdfFile);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(pdfFile);
            string uniqueFileName = $"{fileName}_{Guid.NewGuid()}.inp";
            string inpFilePath = System.IO.Path.Combine(outputPath, uniqueFileName);
            SaveOrderItemsToFile(inpFilePath, orderItems);

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
static (List<OrderItem>, int) ExtractOrderItemsFromPdf(string pdfFilePath)
{
    List<OrderItem> orderItems = new List<OrderItem>();
    var numberOfPages = 0;
    using (PdfReader reader = new PdfReader(pdfFilePath))
    {
        StringWriter output = new StringWriter();
        if (reader.NumberOfPages > 10)
        {
            numberOfPages = reader.NumberOfPages;
            return (new(), reader.NumberOfPages);
        }


        for (int i = 1; i <= reader.NumberOfPages; i++)
        {
            string pageText = PdfTextExtractor.GetTextFromPage(reader, i);
            string[] lines = pageText.Split('\n');

            foreach (string line in lines)
            {
                var match = Regex.Match(line, @"^(?<Name>.+?)\s*(?<Quantity>\d+)\s+(?<Price>\d+(\.\d{1,2})?)$");

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

    return (orderItems, numberOfPages);
}

// Method to save order items to a file
static void SaveOrderItemsToFile(string filePath, List<OrderItem> orderItems)
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
        content.AppendLine($"S,1,______,_,__;{item.Name};{item.Price};{item.Quantity};1;1;5;0;{rand.Next(100000)};0;0;");
    }
    if (orderItems.Count > 0)
    {
        content.AppendLine("T,1,______,_,__;0");

        // Write the content to the file
        File.WriteAllText(filePath, content.ToString());
    }
}