foreach (var ttQuoteMtlR in (from ttQuoteMtlRow in ttQuoteMtl
								where ttQuoteMtlRow.Company == Session.CompanyID
								select ttQuoteMtlRow))
{	
	var quoteHedRow = (from quoteHedR in Db.QuoteHed
		  					where quoteHedR.Company == Session.CompanyID
								&& quoteHedR.QuoteNum == ttQuoteMtlR.QuoteNum
					  		select quoteHedR).FirstOrDefault();
	if (quoteHedRow != null)
	{
		if (Convert.ToBoolean(quoteHedRow["CheckBox02"]) || Convert.ToBoolean(quoteHedRow["CheckBox01"]) == false)
		{
			if (ttQuoteMtlR.RowMod == "A" || ttQuoteMtlR.RowMod == "U")
			{		
				//Validate if parent part is not catalog
				var quoteAsmblR = (from quoteAsmblRow in Db.QuoteAsm
										where quoteAsmblRow.Company == Session.CompanyID
											&& quoteAsmblRow.QuoteNum == ttQuoteMtlR.QuoteNum
											&& quoteAsmblRow.QuoteLine == ttQuoteMtlR.QuoteLine
											&& quoteAsmblRow.AssemblySeq == ttQuoteMtlR.AssemblySeq
										select quoteAsmblRow).FirstOrDefault();
				if (quoteAsmblR != null)
				{
					var partRA = (from partRow in Db.Part
										where partRow.Company == Session.CompanyID
											&& partRow.PartNum == quoteAsmblR.PartNum
										select partRow).FirstOrDefault();
					if (partRA == null)
					{
						callContextBpmData.Number01 = ttQuoteMtlR.QuoteNum;
						callContextBpmData.Number02 = ttQuoteMtlR.QuoteLine;
					}
				}
			}
			if (ttQuoteMtlR.RowMod == "D")
			{
				callContextBpmData.Number01 = ttQuoteMtlR.QuoteNum;
				callContextBpmData.Number02 = ttQuoteMtlR.QuoteLine;
			}
		}
	}
}