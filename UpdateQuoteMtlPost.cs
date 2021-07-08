decimal unitWeight = 0;
decimal totalWeight = 0;

if (callContextBpmData.Number01 != 0 && callContextBpmData.Number02 != 0)
{
	foreach (var quoteMtlR in (from quoteMtlRow in Db.QuoteMtl
								where quoteMtlRow.Company == Session.CompanyID
									&& quoteMtlRow.QuoteNum == callContextBpmData.Number01
									&& quoteMtlRow.QuoteLine == callContextBpmData.Number02
									&& quoteMtlRow.AssemblySeq == 0
								select quoteMtlRow))
	{
		var partR = (from partRow in Db.Part
						where partRow.Company == Session.CompanyID
							&& partRow.PartNum == quoteMtlR.PartNum
						select partRow).FirstOrDefault();
		if (partR != null)
		{
			unitWeight = unitWeight + (partR.NetWeight * quoteMtlR.QtyPer);
		}
	}
	
	var quoteDtlR = (from quoteDtlRow in Db.QuoteDtl
						where quoteDtlRow.Company == Session.CompanyID
							&& quoteDtlRow.QuoteNum == callContextBpmData.Number01
							&& quoteDtlRow.QuoteLine == callContextBpmData.Number02
						select quoteDtlRow).FirstOrDefault();
	if (quoteDtlR != null)
	{
		quoteDtlR["Number06"] = unitWeight;
	}

	foreach (var quoteDtlT in (from quoteDtlRow in Db.QuoteDtl
								where quoteDtlRow.Company == Session.CompanyID
							      && quoteDtlRow.QuoteNum == callContextBpmData.Number01
								select quoteDtlRow))
	{
		totalWeight = totalWeight + (quoteDtlT.SellingExpectedQty * Convert.ToDecimal(quoteDtlT["Number06"]));
		
	}
	
	var quoteHedR = (from quoteHedRow in Db.QuoteHed
						where quoteHedRow.Company == Session.CompanyID
							&& quoteHedRow.QuoteNum == callContextBpmData.Number01
						select quoteHedRow).FirstOrDefault();
	if (quoteHedR != null)
	{
		quoteHedR["Number09"] = totalWeight;
	}

	callContextBpmData.Number01 = 0;
	callContextBpmData.Number02 = 0;
}