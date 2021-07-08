decimal totalWeight = 0;
int iOrderNum = orderNum;
string strOrderNum = orderNum.ToString();
string strQuoteNum = string.Empty;
int iQuoteNum = 0;
Ice.Tables.UD02 ud02;

var orderHedR = (from orderHedRow in Db.OrderHed
						where orderHedRow.Company == Session.CompanyID
							&& orderHedRow.OrderNum == iOrderNum
						select orderHedRow).FirstOrDefault();
if (orderHedR != null)
{
	foreach(var orderDtlR in (from orderDtlRow in Db.OrderDtl
				  					where orderDtlRow.Company == Session.CompanyID
					 					&& orderDtlRow.OrderNum == orderHedR.OrderNum										    
									select orderDtlRow))
	{
		var quoteDtlR = (from quoteDtlRow in Db.QuoteDtl
		                 where quoteDtlRow.Company == Session.CompanyID
		                    && quoteDtlRow.QuoteNum == orderDtlR.QuoteNum
		                    && quoteDtlRow.QuoteLine == orderDtlR.QuoteLine
		                 select quoteDtlRow).FirstOrDefault();
		if (quoteDtlR != null)
		{ 
			orderDtlR["Number01"] = Convert.ToDecimal(quoteDtlR["Number01"]);	// CAM Job Line
			orderDtlR["Number02"] = Convert.ToDecimal(quoteDtlR["Number02"]);	// Original Price
			orderDtlR["Number03"] = Convert.ToDecimal(quoteDtlR["Number03"]);	// Cost
			orderDtlR["Number04"] = Convert.ToDecimal(quoteDtlR["Number04"]);	// Total Line Cost
			orderDtlR["Number05"] = Convert.ToDecimal(quoteDtlR["Number05"]);	// Inc Freight
			orderDtlR["Number06"] = Convert.ToDecimal(quoteDtlR["Number06"]);	// Unit Net Weight
			totalWeight = totalWeight + (orderDtlR.SellingQuantity * Convert.ToDecimal(orderDtlR["Number06"]));
			strQuoteNum = quoteDtlR.QuoteNum.ToString();
			iQuoteNum = quoteDtlR.QuoteNum;
		}
	}

	var quoteHedR = (from quoteHedRow in Db.QuoteHed
	                 where quoteHedRow.Company == Session.CompanyID
											&& quoteHedRow.QuoteNum == iQuoteNum
	                 select quoteHedRow).FirstOrDefault();
	if (quoteHedR != null)
	{ 
		orderHedR["Number01"] = Convert.ToDecimal(quoteHedR["Number01"]);	// Included Freight
		orderHedR["Number02"] = Convert.ToDecimal(quoteHedR["Number02"]);	// Target Total
		orderHedR["Number03"] = Convert.ToDecimal(quoteHedR["Number03"]);	// Max Qty
		orderHedR["Number04"] = Convert.ToDecimal(quoteHedR["Number04"]);	// Target GM%
		orderHedR["Number05"] = Convert.ToDecimal(quoteHedR["Number05"]);	// Original Gross Value
		orderHedR["Number06"] = Convert.ToDecimal(quoteHedR["Number06"]);// Total Order Cost
		orderHedR["Number07"] = Convert.ToDecimal(quoteHedR["Number07"]);// Discount %
		orderHedR["Number08"] = Convert.ToDecimal(quoteHedR["Number08"]);// GM %
		orderHedR["Number09"] = totalWeight;		
		orderHedR["CamJobFile_c"] = quoteHedR["ShortChar01"].ToString();	// CAM Job
		orderHedR["ShortChar02"] = quoteHedR["ShortChar02"].ToString();		// PG Price List
		orderHedR["ShortChar03"] = quoteHedR["ShortChar03"].ToString();	// Quote Type
		orderHedR["ShortChar04"] = quoteHedR["ShortChar04"].ToString();		// Ordered by


		//Delete UD02 records related to PG Price List BEGIN
		foreach (var ud02D in (from ud02Row in Db.UD02
							   				  where ud02Row.Company == Session.CompanyID
								  					&& ud02Row.Key1 == "SalesOrder"
								  					&& ud02Row.Key2 == strOrderNum
							   					select ud02Row))
		{
			ud02 = ud02D;
			Db.UD02.Delete(ud02);
		}
		//Delete UD02 records related to PG Price List END

		//Create UD02 records related to PG Price List BEGIN
	  foreach (var ud02R in (from ud02Row in Db.UD02
	                         where ud02Row.Company == Session.CompanyID
	                            && ud02Row.Key1 == "Quote"
								&& ud02Row.Key2 == strQuoteNum
	                         select ud02Row))
	  {
	  	ud02 = new Ice.Tables.UD02();	    
	    ud02.Company 		= Session.CompanyID;
	    ud02.Key1 			 = "SalesOrder";
	    ud02.Key2 			 = iOrderNum.ToString();
	    ud02.Key3 			 = ud02R.Key3;
	    ud02.Key4 			 = ud02R.Key4;
	    ud02.Key5 			 = ud02R.Key5;
	    ud02.Number01 	 = ud02R.Number01;
	    ud02.ShortChar01 = ud02R.ShortChar01;
	    ud02.Number02 	 = ud02R.Number02;
	    ud02.Number03 	 = ud02R.Number03;
	    ud02.CheckBox01  = ud02R.CheckBox01;
	    ud02.Number04 	 = ud02R.Number04;
	    ud02.CheckBox02  = ud02R.CheckBox02;
	    ud02.ShortChar02 = ud02R.ShortChar02;
		
		Db.UD02.Insert(ud02);	
	  }
		Db.Validate();
	  //Create UD02 records related to PG Price List END
	}	
}

// Call GetRows to Recalculate Sales Order
var UD01DataSet = new  Ice.Tablesets.UD01Tableset();
Ice.Contracts.UD01SvcContract hUD01 = null;
hUD01 = Ice.Assemblies.ServiceRenderer.GetService<Ice.Contracts.UD01SvcContract>(Db);
string whereClauseUD01 = "Key1 = 'OrderRecalc' BY Key1";
string whereClauseUD01Attch = "";
int		pageSize = 0;
int		absPage	= 0;
bool	morePages = false;

if (hUD01 != null)
{
	callContextBpmData.Number01 = orderNum;
	hUD01.GetRows(whereClauseUD01, whereClauseUD01Attch, pageSize, absPage, out morePages);
	callContextBpmData.Number01 = 0;
    hUD01.Dispose();
}