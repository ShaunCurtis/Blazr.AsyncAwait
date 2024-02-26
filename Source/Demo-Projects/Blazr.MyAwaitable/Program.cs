using Blazr.Async;
using System.Runtime.CompilerServices;


Utilities.WriteToConsole($"Main started - click to continue");

Console.ReadLine();

//var task = Task.Run(() => {
//    Utilities.WriteToConsole($"Scheduled Task");
//});

var task = Task.CompletedTask;


task.Start();


Utilities.WriteToConsole($"Main Complete");
