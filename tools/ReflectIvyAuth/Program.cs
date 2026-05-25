using System.Reflection;
using Ivy;

var m = typeof(AuthService).GetMethod("LoginAsync", [typeof(string), typeof(string), typeof(CancellationToken)])!;
foreach (var mi in m.GetMethodBody()!.LocalVariables)
    Console.WriteLine($"local {mi.LocalType.Name}");
// dump IL
var body = m.GetMethodBody()!;
var il = m.GetMethodBody() is null ? "" : "has body";
Console.WriteLine($"MaxStack={body.MaxStackSize} LocalCount={body.LocalVariables.Count}");

foreach (var t in typeof(LoginResult).GetProperties())
    Console.WriteLine($"LoginResult.{t.Name}: {t.PropertyType.Name}");
