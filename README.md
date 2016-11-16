# Tabular LINQPad Driver

[![Build status](https://ci.appveyor.com/api/projects/status/xvai2lkmhgdn973h?svg=true)](https://ci.appveyor.com/project/edberg/tabularlinqpaddriver)

This is a linqpad driver for connecting to SSAS tabular instances.

---

To install:

1) Download the driver (TabularLinqPadDriver.lpx) from the [latest release](https://github.com/edberg/TabularLinqPadDriver/releases/latest)

2) In Linqpad, click "Add connection".

3) In the "Choose Data Context" dialog, press the "View more drivers..." button.

4) In the "Choose a Driver" dialog, press the "Browse" button and select the file downloaded in step 1.

5) Back in the "Choose Data Context" dialog, select "SSAS Tabular" and click the next button.

6) In the SSAS Tabular connection dialog, supply your connection information.

7) You're done. You can write some code against your SSAS database now. For example:

```c#
	var titles =	from customer in Customer
					where customer.TotalCarsOwned > 0 && customer.Gender == "F"
					select new
					{
						Orders = customer.Internet_Distinct_Count_Sales_Order(),
						Name = customer.FirstName + " " + customer.LastName,
						Cars = customer.TotalCarsOwned,
						Sales = customer.Internet_Total_Sales()
					};
	titles.Dump();
```
