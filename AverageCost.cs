using (var txScope = IceContext.CreateDefaultTransactionScope())
{
var ttQuoteHedR = (from ttQuoteHedRow in ds.QuoteHed
          where ttQuoteHedRow.Company == Session.CompanyID
          && Convert.ToBoolean(ttQuoteHedRow["CheckBox01"])
          select ttQuoteHedRow).FirstOrDefault();
          
if (ttQuoteHedR != null)
{
foreach (var quoteDtlR in (from quoteDtlRow in Db.QuoteDtl
                where quoteDtlRow.Company == Session.CompanyID
                && quoteDtlRow.QuoteNum == ttQuoteHedR.QuoteNum 
                select quoteDtlRow))
    if (quoteDtlR.MfgDetail = false && quoteDtlR.LineDesc.Contains("STCK"))
    {
      return;
    }
    else if(quoteDtlR.MfgDetail = true && !quoteDtlR.LineDesc.Contains("STCK"))
    {
      quoteDtlR["ReadyToQuote"] = false;
      
      foreach (var quoteMtlR in (from quoteMtlRow in Db.QuoteMtl
                    where quoteMtlRow.Company == Session.CompanyID
                    && quoteMtlRow.QuoteNum == quoteDtlR.QuoteNum
                    && quoteMtlRow.QuoteLine == quoteDtlR.QuoteLine
                    select quoteMtlRow))
                    {
                      var partCostR = (from partCostRow in Db.PartCost
                              where partCostRow.Company == Session.CompanyID
                              && partCostRow.PartNum == quoteMtlR.PartNum
                              select partCostRow).FirstOrDefault();
                      if (partCostR != null)
                      {                        
                        quoteMtlR["EstUnitCost"] = partCostR.StdBurdenCost + partCostR.StdLaborCost + partCostR.StdMtlBurCost + partCostR.StdSubContCost + partCostR.StdMaterialCost;                       
                      }
                    }
       quoteDtlR["ReadyToQuote"] = true;
    }
}  
Db.Validate();
txScope.Dispose();
}