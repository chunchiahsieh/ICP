using System.Net;
using System.Net.Mail;
using System.Text.Json;
using ICP.Models.NotificationMail;
using ICP.Services.NotificationMail;
using Microsoft.Extensions.Configuration;

int checks=0;
void Check(bool condition,string name) {if(!condition) throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var options=new NotificationSmtpOptions();
Check(options.FromAddress=="TETIS87GEMSAdmin@tel.com","missing sender uses fallback");
foreach(var blank in new[]{"", "  "}) {options.FromAddress=blank;Check(options.FromAddress==NotificationSmtpOptions.DefaultFromAddress,"blank sender uses fallback");}
options=JsonSerializer.Deserialize<NotificationSmtpOptions>("{\"FromAddress\":null}")!;
Check(options.FromAddress==NotificationSmtpOptions.DefaultFromAddress,"null sender uses fallback");
options.FromAddress=" system@tel.com ";
Check(options.FromAddress=="system@tel.com","existing sender preserved and trimmed");
using(var client=NotificationSmtpClient.CreateClient(options)) {
 Check(client.Host=="smtp.tel.co.jp","shared component uses TEL SMTP host");
 Check(client.Port==25,"TEL SMTP uses port 25");
 Check(!client.EnableSsl,"TLS disabled per reference settings");
 Check(!client.UseDefaultCredentials && client.Credentials is null,"no authentication when username absent");
 Check(client.DeliveryMethod==SmtpDeliveryMethod.Network,"network SMTP delivery");
}
options.Host="test.invalid";options.Port=587;options.EnableSsl=true;options.UserName="test-user";options.Password="test-password";
using(var client=NotificationSmtpClient.CreateClient(options)) {
 Check(client.Host=="test.invalid" && client.Port==587 && client.EnableSsl,"explicit SMTP settings remain configurable");
 var credential=client.Credentials!.GetCredential("test.invalid",587,"smtp");
 Check(credential!.UserName=="test-user" && credential.Password=="test-password","explicit credentials supported");
}
var configured=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
 ["NotificationMail:Smtp:FromAddress"]="",["NotificationMail:DeliveryDelay:MailTo:0"]="recipient@example.test",["NotificationMail:DeliveryDelay:CcTo:0"]="cc@example.test"
}).Build().GetSection("NotificationMail").Get<NotificationMailOptions>()!;
Check(configured.Smtp.FromAddress==NotificationSmtpOptions.DefaultFromAddress,"configuration binder blank sender resolves fallback for worker guards");
Check(configured.DeliveryDelay.MailTo.SequenceEqual(new[]{"recipient@example.test"})&&configured.DeliveryDelay.CcTo.SequenceEqual(new[]{"cc@example.test"}),"recipient settings preserved");
foreach(var path in Directory.GetFiles("ICP","appsettings*.json")) {
 var text=File.ReadAllText(path);
 var smtp=System.Text.RegularExpressions.Regex.Match(text,"\"Smtp\"\\s*:\\s*(\\{[^}]*\\})").Groups[1].Value;
 var actual=JsonSerializer.Deserialize<NotificationSmtpOptions>(smtp)!;
 Check(actual.Host=="smtp.tel.co.jp"&&actual.Port==25&&!actual.EnableSsl,Path.GetFileName(path)+" uses TEL relay settings");
 Check(!string.IsNullOrWhiteSpace(actual.FromAddress),Path.GetFileName(path)+" has effective sender");
}
Console.WriteLine($"{checks} checks passed; no SMTP connection or email sent.");
