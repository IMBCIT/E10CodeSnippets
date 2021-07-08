var ttQuoteHedR = (from ttQuoteHedRow in ds.QuoteHed
				  where ttQuoteHedRow.Company == Session.CompanyID
				  select ttQuoteHedRow).FirstOrDefault();
				  
if (ttQuoteHedR != null)
{
	foreach (var quoteDtlR in (from quoteDtlRow in Db.QuoteDtl
                              where quoteDtlRow.Company == Session.CompanyID
                              && quoteDtlRow.QuoteNum == ttQuoteHedR.QuoteNum							  
                              select quoteDtlRow))
							  
		if(quoteDtlR.MfgDetail = false && quoteDtlR.LineDesc.Contains("STCK"))
		{
			return;
		}
		else if(quoteDtlR.MfgDetail = true && !quoteDtlR.LineDesc.Contains("STCK"))
		{
			quoteDtlR["ReadyToQuote"] = false;
			
			foreach (var quoteOpDtlR in (from quoteOpDtlRow in Db.QuoteOpDtl
									where quoteOpDtlRow.Company == Session.CompanyID
									&& quoteOpDtlRow.QuoteNum == quoteDtlR.QuoteNum
									&& quoteOpDtlRow.QuoteLine == quoteDtlR.QuoteLine
									&& quoteOpDtlRow.ResourceGrpID != "RPRT"
									select quoteOpDtlRow))
		
					{
						var quoteHedR = (from quoteHedRow in Db.QuoteHed
										where quoteHedRow.Company == Session.CompanyID
										&& quoteHedRow.QuoteNum == quoteOpDtlR.QuoteNum
										select quoteHedRow).FirstOrDefault();
			
						if (quoteHedR != null)
						{	
						quoteOpDtlR["OverrideRates"] = true;
						quoteOpDtlR["ProdLabRate"] = Convert.ToDecimal(quoteHedR["PrevWageRate_c"]);
						}
					}
		}
		
foreach (var quoteOp in (from quoteOpRow in Db.QuoteOpr
					where quoteOpRow.Company == Session.CompanyID
					&& quoteOpRow.QuoteNum == ttQuoteHedR.QuoteNum
					select quoteOpRow))
					
	if (quoteOp != null)
	{
		quoteOp["LaborEntryMethod"] = "T";	
	}
turned off until later date

	
foreach (var quoteDtl in (from quoteDtRow in Db.QuoteDtl
				where quoteDtRow.Company == Session.CompanyID
				&& quoteDtRow.QuoteNum == ttQuoteHedR.QuoteNum
				select quoteDtRow))
				
	if(quoteDtl != null)
	{
		quoteDtl["ReadyToQuote"] = true;
	}	
}