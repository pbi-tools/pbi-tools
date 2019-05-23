<Query Kind="Statements" />

System.Net.WebUtility.UrlEncode("Win/Loss").Dump("Encode");
System.Net.WebUtility.UrlDecode("Win%2FLoss").Dump("Decode");
new String(System.IO.Path.GetInvalidPathChars()).Dump("InvalidPathChars");
new String(System.IO.Path.GetInvalidFileNameChars()).Dump("InvalidFileNameChars");