using Core.Utilities.Results;

var url = "https://api.gumusmakas.com.tr/uploads/social-media/test.mov";
var r = new SuccessDataResult<string>(url);
Console.WriteLine($"Data={(r.Data ?? "NULL")}");
Console.WriteLine($"Message={(r.Message ?? "NULL")}");
