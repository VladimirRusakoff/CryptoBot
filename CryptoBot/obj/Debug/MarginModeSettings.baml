   M S B A M L   `   `   `  ????D  @CryptoBot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:CryptoBot	CryptoBot  5   	   "    CryptoBot.MarginModeSettings   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   )localclr-namespace:CryptoBot     5   	     ? Title$  Settings of Margin Type??5   	   $	??450??6)   $	??800??66    ? DataContext 5	        CryptoBot.ViewModelMM  5
   
   .??5      ? 
??????? ? RowDefinitions 5   
   ?? 5      $??40 ?6   ?? 5      .+?5   
   ? $??0??6   .+?5      ?? $??20??6   $	??100??6!   $
??Fill??6-   ??6<   ?? *
buttonFill+?? 5      $??20??6   $	??100??6!   $??
Select all??6-   ??6B   ?? *buttonSelectAll+?? 5      $??30??6   $??Margin Type??6    ?? 5      -   5         ??Name$ cbMarginType??5   /   $??20??6   $	??100??6#    ?? Text 6E   ?? *formMarginType+?? 5      -   5         ??Name$ 
tbLeverage??5   -   $??20??6   $??20??6"   ??6A   ?? *formLeverage+?? 5      $??30??6   $??X??6    ?? 5      $??20??6   $	??100??6!   $??Change??6-   ??6>   ?? *buttonChange+5      ?? 5   
   $??1??6   ??6    ?? *symbLeverage+ ?? View 5      ?? 5      .P?5      ?? $??Select??6%    ?? CellTemplate 5      ?? 5       .R?5!   "   ?? $
??5, 0q?6+    d? 	IsChecked 69   ?? *
isSelect+5"      5$      ?? 5%      $??Symbols??6%   	 ?? Width$		 120??6^   
 ?? DisplayMemberBinding
 66   ?? *symbol+6^   ?? 5&      $??Margin Mode??6%   $		 100??6c   
 6:   ?? *	marMode+6c   ?? 5'      $??Leverage??6%   
 67   ?? *
Leverage+5(      5*      5+      5,      