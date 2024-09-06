using iTextSharp.text.pdf.parser;
using PdfToInp;
using iTextSharp.text.pdf;
using System.Text;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

bool exitRequested = false;

while (!exitRequested)
{
	string downloadsDirectoryPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

	// Get user input with a timeout
	string directoryPath = await GetDirectoryPathWithTimeout("Shënoni vendin ku ruhet fatura (ku bëhet download): ", downloadsDirectoryPath);
	string outputPath = await GetDirectoryPathWithTimeout("Shënoni vendin ku konvertohet fatura (folderi i printerit): ", System.IO.Path.GetTempPath());

	Console.WriteLine("Duke punuar... Shtypni R për të restartuar programin ose Q për të përfunduar.");

	// Create a cancellation token to stop the monitor thread when needed
	CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
	CancellationToken cancellationToken = cancellationTokenSource.Token;

	// Start monitoring and input listening in separate threads
	Thread monitorThread = new(() => MonitorDirectory(directoryPath, outputPath, cancellationToken));
	Thread inputThread = new(() => ListenForRestartOrQuit(cancellationTokenSource));

	monitorThread.Start();
	inputThread.Start();

	// Wait for the input thread to finish before proceeding (if user presses R to restart)
	inputThread.Join();

	// Stop the monitor thread by requesting cancellation
	cancellationTokenSource.Cancel();

	// Wait for the monitor thread to acknowledge the cancellation and finish
	monitorThread.Join();

	// If the user requested to exit (by pressing 'Q'), exit the loop
	if (exitRequested)
	{
		break;
	}
}

// Method to get a directory path with a timeout
static async Task<string> GetDirectoryPathWithTimeout(string prompt, string defaultPath)
{
	string input = defaultPath;

	var inputTask = Task.Run(() =>
	{
		Console.WriteLine(prompt);
		input = Console.ReadLine();
	});

	var timeoutTask = Task.Delay(10000); // 5 seconds timeout

	var completedTask = await Task.WhenAny(inputTask, timeoutTask);

	if (completedTask == inputTask)
	{
		// User input received within timeout
		if (!Directory.Exists(input) && !string.IsNullOrWhiteSpace(input))
		{
			Console.WriteLine("Folderi i dhënë nuk është i vlefshëm. Përdorim folderin e përkohshëm.");
			input = defaultPath;
		}
	}
	else
	{
		// Timeout occurred
		Console.WriteLine("Koha e skaduar për input. Përdorim folderin default(Downloads ose Temp).");
		input = defaultPath;
	}

	return input;
}

// Monitoring directory logic
static void MonitorDirectory(string directoryPath, string outputPath, CancellationToken cancellationToken)
{
	while (!cancellationToken.IsCancellationRequested)
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
					if(orderItems.Count > 0)
						File.Delete(pdfFile);
			}
			catch (Exception ex)
			{
				// Handle any exceptions that occur during processing
				Console.WriteLine($"Error processing {pdfFile}: {ex.Message}");
			}
		}

		// Sleep for 2 seconds before checking again
		Thread.Sleep(2000);
	}
}

// Method to listen for restart or quit input (pressing 'R' to restart or 'Q' to quit)
static void ListenForRestartOrQuit(CancellationTokenSource cancellationTokenSource)
{
	while (true)
	{
		ConsoleKeyInfo keyInfo = Console.ReadKey(true);
		if (keyInfo.Key == ConsoleKey.R)
		{
			Console.WriteLine("\nRestarting program...");
			break;  // Exit the input thread to restart the program
		}
		else if (keyInfo.Key == ConsoleKey.Q)
		{
			Console.WriteLine("\nExiting program...");
			cancellationTokenSource.Cancel(); // Signal cancellation for monitor thread
			Environment.Exit(0);  // Gracefully exit the program
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
	if(orderItems.Count > 0)
	{
		content.AppendLine("T,1,______,_,__;0");

		// Write the content to the file
		File.WriteAllText(filePath, content.ToString());
	}
}
