   M S B A M L   `   `   `  ???? D  @CryptoBot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:CryptoBot	CryptoBot      CryptoBot.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     )localclr-namespace:CryptoBot       ? Title$  
CRYPTO BOT??$	??770??$	??820??.??? 
???   ? ? ColumnDefinitions ?? $
??152* ??? $
??371* ?.+??? $	??400??$??	15,1,15,0q?
???????$	??Top=? ?? Padding$ 30q? ?? CornerRadius$ 20??$??2??._??? .+??? -    ??Name$ 	lbLicense??$??Right=?$?? ??$??14(?$
??Bold"?? $??0 0 0 20q?.+??? $
??Left=?$??BINANCE FUTURES??$??20(?$
??Bold"??? $??Center=?$??Nickname??$??15(??? -    ??Name$ 
tbNickname??$??Right=?$??15(?$	??310?????	#FFADABAB$
??Bold"?? $??0 0 0 10q?.+??? $
??Left=?$??API Key???? -   $ tbAPIKey??$??Right=?$	??600??? .+??? $
??Left=?$??
Secret Key??G? -    G?Name$ pbSecKey??$??Right=?$	??600???? $	??400??$??
15,0,15,15q?
???????$??Center=?$ 10q?$ 20??$??2??._??? .+?? .+??? -    ??Name$ 	buttonAdd??$	??Add???? -   $ buttonDelete??$??Delete??7  1System.Windows.Forms.Integration.WindowsFormsHost  -     Name$ HostTemp??$	??190??$	??Top=?? .+?? .+??? -   	 ??Name$	 cbUseBTCdelta??$??Use BTCUSDT Delta | ??$??Center=??? $??BTC Minutes: ???? -	   $ tbBTCminutes??$??40???? $??BTC Delta: ???? -
   $ 
tbBTCdelta??$??40???? $??Seconds for waiting for: ???? -   $ tbBTCsecondsOff??$??40??? $??0 5 0 0q?.+??? -   $
??Left=?$
??TEST??$y?Hidden=??? -   $ Start??$??Center=?$??16(?$??Start strategy??$??25??$??328,0,327.8,-0.4q?$	??Top=??? -   $	 	cbLogIsOn??$??Right=?$??Center=?$	??Log???? $??
15 0 15 15q?
???????$??Bottom=?$ 10q?$ 20??$??2??._??? -   
 ??Name$
 
tcBalances??$	??180??.@??? -    ??Name$ Settings??????  ?? Orientation$ 
Horizontal=?.+?A? $??10??$??10??
???   ??? $??3q?
Settings???? -   $ MarginMode_Click??$??30??$	??100??$
??Left=?$??Margin Mode??