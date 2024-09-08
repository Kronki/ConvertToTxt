using iTextSharp.text.pdf.parser;
using PdfToInp;
using iTextSharp.text.pdf;
using System.Text;
using System;
using System.Collections.Generic;
using System.Threading;

bool exitRequested = false;

// Get the directory of the .sln file (assuming the .sln is in the current working directory)
string solutionDirectory = AppContext.BaseDirectory;

while (!exitRequested)
{
    string directoryPath = solutionDirectory;  // Use the solution directory as the input path
    string outputPath = solutionDirectory;     // Use the solution directory as the output path

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
            if (pages < 10)
                if (orderItems.Count > 0)
                    File.Delete(pdfFile);
        }
        catch (Exception ex)
        {
            // Handle any exceptions that occur during processing
            Console.WriteLine($"Error processing {pdfFile}: {ex.Message}");
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

        var orderId = "";

        for (int i = 1; i <= reader.NumberOfPages; i++)
        {
            string pageText = PdfTextExtractor.GetTextFromPage(reader, i);
            string[] lines = pageText.Split('\n');

            foreach (string line in lines)
            {
                // Match lines that contain the order items
                string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && parts[1].Contains('#'))
                    orderId = parts[1];
                if (parts.Length == 3 && decimal.TryParse(parts[2], out decimal price) && int.TryParse(parts[1], out int quantity))
                {
                    orderItems.Add(new OrderItem
                    {
                        Name = parts[0],
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
        content.AppendLine($"S,1,______,_,__;{item.Name};{item.Price};{item.Quantity};1;1;5;0;{rand.Next(100)};0;0;");
    }
    if (orderItems.Count > 0)
    {
        content.AppendLine("T,1,______,_,__;0");

        // Write the content to the file
        File.WriteAllText(filePath, content.ToString());
    }
}
