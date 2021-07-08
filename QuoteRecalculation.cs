// #region key
//
// 0 - declaration of variables.
// 1 - Get UD02Table for quote.
// 2 - Get the quote data.
// 3 - Update the costs on the quote.
// 4 - Extract the quote hed row from the quote data.
// 5 - Get ud01tablest and ud01tablecust.
// 5.1 - 
//    5.1A - Loop over quote dtls to get Total Order Cost.
//    5.1B - If GM% is 0, find it using Total Order Cost from 5.1A and TargetTotal.
// 6 - 
//    6A - Set lineNum04LineCost
//    6B - DELETED handled by 5.1A now.
//    6C - Set Price List on Lines
//    6D - Restore Unit Prices
//    6E - Apply Margin %
//    6F - Accumulate hedNum05OrigGross
//    6G - Set fields on line
//    6H - Accumulate total gross
//    6I - Set rowmod on dtl to "U"
//  7 - Allocate buried freight
//  8 - Adjust gross value
//  9 - Set "recalc" fields
//  10 - Set quotehed row mod to "U"
//  11 - Update the quote

#region 0
long elapsedTicks = DateTime.Now.Ticks;
TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);

var quoteDS = new Erp.Tablesets.QuoteTableset();
Erp.Contracts.QuoteSvcContract hQuote = null;
hQuote = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.QuoteSvcContract>(Db);

int quoteNum = 0;
string strQuoteNum = "";
string custNum = "";
string shipToNum = "";
decimal tempGross = 0;  // for Included Freight
decimal tempAGross = 0; // for Total Adjustment
decimal mra = 0;
decimal totalGross = 0;  // Total Gross charges
decimal lineMRA = 0;

decimal  lineNum02OrigPrice = 0;
decimal  lineNum03Cost = 0;
decimal  lineNum04LineCost = 0;
decimal  lineNum05IncFr = 0;
string   linePriceListCode = "";
string   lineBreakListCode = "";
decimal  lineDocExpUnitPrice = 0;
bool     lineOverridePriceList = false;
bool     lineRefreshQty = false;

decimal  hedNum05OrigGross = 0;
decimal  hedNum06TotalOrderCost = 0;

var log = new StringBuilder();
var isLogging = false;

#endregion

using (var tx = IceDataContext.CreateDefaultTransactionScope())
{  
  if (whereClauseUD01 == "Key1 = 'QuoteRecalc' BY Key1" && callContextBpmData.Number01 != 0)
  {
    try
    {
      quoteNum = Convert.ToInt32(callContextBpmData.Number01);  
      strQuoteNum = callContextBpmData.Number01.ToString();

#region 1

      var ud02Table = Db.UD02.Where(u=>u.Company == Session.CompanyID && u.Key1 == "Quote" && u.Key2 == strQuoteNum && u.Key3 != string.Empty && u.Key4 != string.Empty && u.Key5 == string.Empty);

#endregion

#region 2
      quoteDS = hQuote.GetByID(quoteNum);
#endregion
        
#region 3
      
      hQuote.UpdateCosts(quoteNum, ref quoteDS);
#endregion
  
#region 4

      var qHed = quoteDS.QuoteHed.FirstOrDefault(h=>h.Company == Session.CompanyID && h.QuoteNum == quoteNum);

#endregion

      if (qHed != null)
      {
          
#region 5

          custNum = qHed.CustNum.ToString();
          shipToNum = qHed.ShipToNum.ToString();

          //Found records related to the Customer Ship To
          var ud01TableST = Db.UD01.Where(u=>u.Company == Session.CompanyID && u.Key1 == custNum && u.Key2 == shipToNum && u.Key3 != string.Empty && u.Key4 != string.Empty);

          //Found records related to the Customer 
          var ud01TableCust = Db.UD01.Where(u=>u.Company == Session.CompanyID && u.Key1 == custNum && u.Key2 == string.Empty && u.Key3 != string.Empty && u.Key4 != string.Empty);

#endregion

#region 5.1

#region 5.1A

          var totalOrderCostLessCatalogItems = 0m;
          var totalCatalogUnitPrices = 0m;
          
          var dtls = quoteDS.QuoteDtl.Where(d=>d.Company == Session.CompanyID && d.QuoteNum == quoteNum).ToList();
          var dtlsCount = dtls.Count;

          foreach (var d in dtls)
          {      
              lineNum03Cost      = d.UDField<decimal>("Number03");
              lineNum04LineCost  = d.UDField<decimal>("Number04");

              var isCatalogItem = (d.ProdCode == "STCKCAT" || d.ProdCode == "STCKSPL" || d.ProdCode == "POSTTEN");

              if (isCatalogItem)
              {
                  var partCost = Db.PartCost.FirstOrDefault(p=>p.Company == Session.CompanyID && p.PartNum == d.PartNum);

                  if (partCost != null)
                  { 
                    lineNum03Cost     = partCost.StdMaterialCost;
                    lineNum04LineCost = d.SellingExpectedQty * lineNum03Cost;        

                  }

                  totalCatalogUnitPrices += d.DocExtPriceDtl; //d.DocExpUnitPrice; Changed 10/25/2017

              }
              else
              {    
                  var quoteQty = quoteDS.QuoteQty.FirstOrDefault(q=>q.Company == Session.CompanyID && q.QuoteNum == d.QuoteNum && q.QuoteLine == d.QuoteLine);

                  if (quoteQty != null)
                  {
                    lineNum03Cost     = quoteQty.TotalCost;
                    lineNum04LineCost = d.SellingExpectedQty * lineNum03Cost;
                    
                  } 
              }

              //Accumulate Total Order Cost
              hedNum06TotalOrderCost += lineNum04LineCost;

              // Accumulate Total Order Cost Less Catalog Items
              if (!isCatalogItem)
                totalOrderCostLessCatalogItems += lineNum04LineCost;
          }
#endregion

#region 5.1B

          decimal targetTotal = qHed.UDField<decimal>("Number02");        
          if (targetTotal != 0)
          {
              // set number 04
              log.AppendLine("targetTotal = " + targetTotal.ToString());
              log.AppendLine("totalCatalogUnitPrices = " + totalCatalogUnitPrices.ToString());
              decimal targetTotalLessCatalogUnitPrices = targetTotal - totalCatalogUnitPrices;
              log.AppendLine("targetTotalLessCatalogUnitPrices = " + targetTotalLessCatalogUnitPrices.ToString());

              log.AppendLine("quoteHed.Number04 = targetTotalLessCatalogUnitPrices == 0 ? 0 : ((targetTotalLessCatalogUnitPrices - totalOrderCostLessCatalogItems) / targetTotalLessCatalogUnitPrices) * 100");
              var gmp = targetTotalLessCatalogUnitPrices == 0 ? 0 : ((targetTotalLessCatalogUnitPrices - totalOrderCostLessCatalogItems) / targetTotalLessCatalogUnitPrices) * 100;  

              qHed["Number04"] = gmp < 0 ? gmp * -1 : gmp;
              log.AppendLine("quotehed.Number04 = " + qHed["Number04"].ToString());                      
          }        

#endregion

#endregion
      
#region 6

          var totalCatalogOriginalPrices = 0m;

          foreach (var d in dtls)
          {
          
#region 6A
              lineNum02OrigPrice = 0; //Convert.ToDecimal(ttQuoteDtlR["Number02"]);
              lineNum03Cost      = Convert.ToDecimal(d["Number03"]);
              lineNum04LineCost  = Convert.ToDecimal(d["Number04"]);
              linePriceListCode  = d.PriceListCode;
              lineBreakListCode  = d.BreakListCode;
              lineDocExpUnitPrice = d.DocExpUnitPrice;
              lineOverridePriceList = d.OverridePriceList;                                
              lineRefreshQty = false;
      
              d.DocExpUnitPrice = 0;  


              var isCatalogItem = (d.ProdCode == "STCKCAT" || d.ProdCode == "STCKSPL" || d.ProdCode == "POSTTEN");

  
              // Sets Num03 and Num04 depending on catalog or non catalog
              if (isCatalogItem)
              {
                  //---------------------- BEGIN Calculate Line Item Cost Catalog Parts ---------------------- 
                  var partCostR = (from row in Db.PartCost
									where row.Company == Session.CompanyID
										&& row.PartNum == d.PartNum
									select row).FirstOrDefault();
                  if (partCostR != null)
                  {
                    lineNum03Cost     = partCostR.StdMaterialCost;
                    lineNum04LineCost = d.SellingExpectedQty * lineNum03Cost;        
                  }
              }//End Catalog Item
              else
              {    
                  //---------------------- BEGIN Calculate Line Item Cost not Catalog Parts ---------------------- 
                  var ttQuoteQtyR = (from row in quoteDS.QuoteQty
                                     where row.Company == Session.CompanyID
                                        && row.QuoteNum == d.QuoteNum
                                        && row.QuoteLine == d.QuoteLine
                                     select row).FirstOrDefault();
                  if (ttQuoteQtyR != null)
                  {
                    lineNum03Cost     = ttQuoteQtyR.TotalCost;
                    lineNum04LineCost = d.SellingExpectedQty * lineNum03Cost;
                  }
                  //---------------------- END Calculate Line Item Cost not Catalog Parts ---------------------- 
              }//End Not Catalog Item

#endregion
          
#region 6B

              //Accumulate Total Quote Cost
              // now handled in 5.1
              //hedNum06TotalOrderCost = hedNum06TotalOrderCost + lineNum04LineCost;

#endregion
          
          
              //---------------------- BEGIN If Price List Exists, Set Price List on QuoteDtl -------------------------- 
#region 6C

              var ud02 = (from row in ud02Table
                      where row.Company == Session.CompanyID
                         && row.Key1 == "Quote"
                         && row.Key2 == strQuoteNum
                         && row.Key3 != "" 
                         && row.Key4 == d.ProdCode
                         && row.ShortChar01 != ""
                      select row).FirstOrDefault();
              if (ud02 != null)
              {          
                var customerPriceLstR = (from customerPriceLstRow in Db.CustomerPriceLst
                                         where customerPriceLstRow.Company == Session.CompanyID
                                            && customerPriceLstRow.CustNum == qHed.CustNum
                                            && customerPriceLstRow.ListCode == ud02.ShortChar01
                                         select customerPriceLstRow).FirstOrDefault();
                if (customerPriceLstR != null)
                {
                  var priceLstR = (from priceLstRow in Db.PriceLst
                                   where priceLstRow.Company == Session.CompanyID
                                      && priceLstRow.ListCode == customerPriceLstR.ListCode                    
                                      && priceLstRow.StartDate <= DateTime.Today.Date
                                      && priceLstRow.EndDate >= DateTime.Today.Date
                                   select priceLstRow).FirstOrDefault();
                  if (priceLstR != null)
                  {                
                    var priceLstPartsR = (from priceLstPartsRow in Db.PriceLstParts
                                          where priceLstPartsRow.Company == Session.CompanyID
                                             && priceLstPartsRow.ListCode == priceLstR.ListCode
                                             && priceLstPartsRow.PartNum == d.PartNum
                                          select priceLstPartsRow).FirstOrDefault();
                    if (priceLstPartsR != null && d.PriceListCode != priceLstPartsR.ListCode)
                    {
                      linePriceListCode = priceLstPartsR.ListCode;
                      lineBreakListCode = priceLstPartsR.ListCode;
                      lineDocExpUnitPrice = priceLstPartsR.BasePrice;
                      lineOverridePriceList  = true;                                
                      lineRefreshQty = true;
                    }//End priceLstPartsR
                    else
                    {
                      var priceLstGroupsR = (from priceLstGroupsRow in Db.PriceLstGroups
                                             where priceLstGroupsRow.Company == Session.CompanyID
                                                && priceLstGroupsRow.ListCode == priceLstR.ListCode
                                                && priceLstGroupsRow.ProdCode == d.ProdCode
                                             select priceLstGroupsRow).FirstOrDefault();
                      if (priceLstGroupsR != null && d.PriceListCode != priceLstGroupsR.ListCode)
                      {
                        linePriceListCode = priceLstGroupsR.ListCode;
                        lineBreakListCode = priceLstGroupsR.ListCode;
                        lineDocExpUnitPrice = priceLstGroupsR.BasePrice;
                        lineOverridePriceList = true;                                
                        d.RefreshQty = true;
                      }//End priceLstGroupsR
                    }
                  }//End priceLstR
                }//End customerPriceLstR
              }//End ud02R    
  
#endregion                      
              //---------------------- END If Price List Exists, Set Price List on QuoteDtl -------------------------- 
      
      
              //---------------------- BEGIN Restore Unit Prices (Never runs) ---------------------------------------------- 
#region 6D

              // Never runs because linenum02 is always 0
              if (lineNum02OrigPrice != 0 && isCatalogItem)
              {
                  lineDocExpUnitPrice = lineNum02OrigPrice;
              }

#endregion
              //---------------------- END Restore Unit Prices (Never runs)---------------------------------------------- 
      
      
              //---------------------- BEGIN Apply Margin % (Set DocExpUnitPrice)-------------------------- 
#region 6E
          
              // This condition no longer applies. Target GM% (hed.num04) will always have a value.
              //If Target GM% is zero
              if (Convert.ToDecimal(qHed["Number04"]) == 0)
              {
      
                if (!isCatalogItem)
                {
                  ud02 = (from ud02Row in ud02Table
                          where ud02Row.Company == Session.CompanyID
                             && ud02Row.Key1 == "Quote"
                             && ud02Row.Key2 == strQuoteNum
                             && ud02Row.Key3 != ""
                             && ud02Row.Key4 == d.ProdCode                         
                          select ud02Row).FirstOrDefault();
                  if (ud02 != null)
                  {  
                    //If Galv Mtl is true
                    if (Convert.ToBoolean(d["CheckBox01"]))
                    {
                      lineDocExpUnitPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud02["Number01"]) / 100)));  
                    }
                    else
                    {                  
                      lineDocExpUnitPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud02["Number02"]) / 100)));        
                    }
                  }
                }//End Non Stock Parts        
              }//End Target GM% is zero
      
              //If Target GM% is not zero
              else
              {
                if (isCatalogItem)
                {
                  ud02 = (from ud02Row in ud02Table
                          where ud02Row.Company == Session.CompanyID
                             && ud02Row.Key1 == "Quote"
                             && ud02Row.Key2 == strQuoteNum
                             && ud02Row.Key3 != ""
                             && ud02Row.Key4 == d.ProdCode
                          select ud02Row).FirstOrDefault();
                  if (ud02 != null)
                  {  
                    if (Convert.ToBoolean(ud02["CheckBox01"]))
                    {
                      lineDocExpUnitPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(qHed["Number04"]) / 100)));
                    }        
                  }
                }  
                else // not catalog item
                {
                  lineDocExpUnitPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(qHed["Number04"]) / 100)));
                }
              }//End Target GM% not zero

#endregion
              //---------------------- END Apply Margin % (Set DocExpUnitPrice)----------------------------  
      
      
              //---------------------- BEGIN Get Original Price for QuoteDtl on the fly ---------------------- 
#region 6F

              if (!isCatalogItem && lineNum02OrigPrice == 0)
              {
                  //Found if there is a record related to the Ship To
                  var ud01ST = (from ud01Row in ud01TableST
                        where ud01Row.Company == Session.CompanyID
                           && ud01Row.Key1 == custNum
                           && ud01Row.Key2 == shipToNum
                           && ud01Row.Key3 != ""
                           && ud01Row.Key4 == d.ProdCode
                        select ud01Row).FirstOrDefault();
                  if (ud01ST != null)
                  {
                    //If Galv Mtl is true
                    if (Convert.ToBoolean(d["CheckBox01"]))
                    {
                      lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud01ST["Number01"]) / 100)));  
                    }
                    else
                    {                  
                      lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud01ST["Number02"]) / 100)));
                    }
                  }
                  else
                  {
                    //Found if there is a record related to the Customer
                    var ud01C = (from ud01Row in ud01TableCust
                                 where ud01Row.Company == Session.CompanyID
                                    && ud01Row.Key1 == custNum
                                    && ud01Row.Key2 == ""
                                    && ud01Row.Key3 != ""
                                    && ud01Row.Key4 == d.ProdCode
                                 select ud01Row).FirstOrDefault();
                    if (ud01C != null)
                    {
                      //If Galv Mtl is true
                      if (Convert.ToBoolean(d["CheckBox01"]))
                      {
                        lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud01C["Number01"]) / 100)));  
                      }
                      else
                      {                  
                        lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud01C["Number02"]) / 100)));
                      }
                    }
                    else
                    {
     
                      ud02 = (from ud02Row in ud02Table
                            where ud02Row.Company == Session.CompanyID
                               && ud02Row.Key1 == "Quote"
                               && ud02Row.Key2 == strQuoteNum
                               && ud02Row.Key3 != ""
                               && ud02Row.Key4 == d.ProdCode
                            select ud02Row).FirstOrDefault();
                      if (ud02 != null)                            
                      {
                        //If Galv Mtl is true
                        if (Convert.ToBoolean(d["CheckBox01"]))
                        {
                          lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud02["Number01"]) / 100)));
                        }
                        else
                        {                  
                          lineNum02OrigPrice = (lineNum03Cost / (1 - (Convert.ToDecimal(ud02["Number02"]) / 100)));          
                        }
                      }//End ud02
                    }//End not found Customer record
                  }//End not found ShipTo record          
                }//End If NonStock QuoteDtl
              else
              {
                  lineNum02OrigPrice = d.SellingExpectedQty == 0 ? 0 : d.DocExtPriceDtl / d.SellingExpectedQty;
                  totalCatalogOriginalPrices += (lineNum02OrigPrice * d.SellingExpectedQty);
              }
      
              //Get Original Gross by QuoteDtls    
              hedNum05OrigGross = (hedNum05OrigGross + (lineNum02OrigPrice * d.SellingExpectedQty));
      
#endregion
              //---------------------- END Get Original Price for QuoteDtl on the fly ----------------------
  
#region 6G

              var ttQuoteDtlNew = quoteDS.QuoteDtl.NewRow();
              BufferCopy.Copy(d, ttQuoteDtlNew);
              quoteDS.QuoteDtl.Add(ttQuoteDtlNew);
          
              d["Number02"] = (lineNum02OrigPrice * d.SellingExpectedQty);
              d["Number03"] = (lineNum03Cost);
              d["Number04"] = (lineNum04LineCost);
      
              d.PriceListCode = linePriceListCode;
              d.BreakListCode = lineBreakListCode;
              d.DocExpUnitPrice = lineDocExpUnitPrice;
              d.OverridePriceList = lineOverridePriceList;                                
              d.RefreshQty = lineRefreshQty;  

#endregion

#region 6H
              decimal perDivisor = 1m;  
              if(d.ExpPricePerCode == "C")
                perDivisor = 100m;
              else if (d.ExpPricePerCode == "M")
                perDivisor = 1000m;
              totalGross += Math.Round((lineDocExpUnitPrice/perDivisor) * d.SellingExpectedQty,4);
              log.AppendLine("2 Total Gross [" + totalGross.ToString() + "] = Math.Round(lineDocExpUnitPrice[" + lineDocExpUnitPrice.ToString() + "] * SellingExpectedQty[" + d.SellingExpectedQty.ToString() + "], 4);");

#endregion

#region 6I
  
              d.RowMod = "U";
              // hQuote.GetDtlUnitPriceInfo_User(true, false, false, true, ref quoteDS);    
#endregion
          } //foreach QuoteDlt

#endregion    
      
          //---------------------- BEGIN Allocate Buried Freight ------------------------------------
#region 7

          if (qHed.ShipViaCode == "IF")
          {
              //Get tempGross value
              tempGross = 0;
              foreach (var d in dtls.Where(d=>d.RowMod == "U")) 
              {          
                var ud02 = (from ud02Row in ud02Table
                        where ud02Row.Company == Session.CompanyID
                           && ud02Row.Key1 == "Quote"
                           && ud02Row.Key2 == strQuoteNum
                           && ud02Row.Key3 != ""
                           && ud02Row.Key4 == d.ProdCode
                           && ud02Row.CheckBox01
                        select ud02Row).FirstOrDefault();
                if (ud02 != null)
                {
                  tempGross = tempGross + (d.DocExpUnitPrice * d.SellingExpectedQty);
                }
              }

              //Calculate Included Freight value
              foreach (var d in (from ttQuoteDtlRow in quoteDS.QuoteDtl
                                           where ttQuoteDtlRow.Company == Session.CompanyID
                                              && ttQuoteDtlRow.QuoteNum == quoteNum
                                              && ttQuoteDtlRow.RowMod == "U"
                                           select ttQuoteDtlRow).ToList())  
              {
                var ud02 = (from ud02Row in ud02Table
                        where ud02Row.Company == Session.CompanyID
                           && ud02Row.Key1 == "Quote"
                           && ud02Row.Key2 == strQuoteNum
                           && ud02Row.Key3 != ""
                           && ud02Row.Key4 == d.ProdCode
                        select ud02Row).FirstOrDefault();
                if (ud02 != null)
                {
                  if(ud02.CheckBox01 && tempGross != 0)  
                  {          
                    lineNum05IncFr = tempGross == 0 ? 0 : ((Convert.ToDecimal(qHed["Number01"]) / tempGross) * (d.DocExpUnitPrice * d.SellingExpectedQty));
                  }
                  else
                  {
                    lineNum05IncFr = Convert.ToDecimal(0);
                  }
      
                  d["Number05"] = (lineNum05IncFr);
                
                  if(d.SellingExpectedQty != 0)
                  {
                    d.DocExpUnitPrice = (((d.DocExpUnitPrice * d.SellingExpectedQty) + lineNum05IncFr) / d.SellingExpectedQty);
                  }
      
                  totalGross = (totalGross + lineNum05IncFr);
      
                }//End ud02
              }//End foreach ttQuoteDtlR
            }//End ShipViaCode = "IF"

#endregion
          //---------------------- END Allocate Buried Freight ------------------------------------
      
#region 8.1

  if (targetTotal != 0)
  {
    var includedFreight = Convert.ToDecimal(qHed["Number01"]);
    var difference = targetTotal - (totalGross - includedFreight);
    var isPlus = difference >= 0;
    var pennies = Math.Abs(difference) * 100; // difference as pennies
    
    //InfoMessage.Publish("Total Gross: " + totalGross.ToString() + " pennies = " + pennies.ToString());

    while(pennies > 0)
    {
      var startingNumberOfPennies = pennies;
      // loop through lines and take off pennies
      foreach (var d in (from ttQuoteDtlRow in quoteDS.QuoteDtl
                                             where ttQuoteDtlRow.Company == Session.CompanyID
                                                && ttQuoteDtlRow.QuoteNum == quoteNum
                                                && !(ttQuoteDtlRow.ProdCode == "STCKCAT"
                                                || ttQuoteDtlRow.ProdCode == "STCKSPL"
                                                || ttQuoteDtlRow.ProdCode == "POSTTEN")
                                                && ttQuoteDtlRow.RowMod == "U"
                                             select ttQuoteDtlRow).ToList())  
      {      
        //InfoMessage.Publish(ttQuoteDtlR.SellingExpectedQty.ToString());
        if (d.SellingExpectedQty <= pennies)
        {
          d.DocExpUnitPrice = isPlus ? d.DocExpUnitPrice + .01m : d.DocExpUnitPrice - .01m;
  
          // take off pennies by the qty
          pennies = pennies - d.SellingExpectedQty;
        }
        else continue;
      }

      if (pennies == startingNumberOfPennies)
      {
        // use a different strategy 
        // leaving this unimplemented unless it comes up :)
        break; // for now exit the loop
      }
    }
  }

#endregion
  
#region 9

//InfoMessage.Publish("Before buffercopy " + qHed["SalesRepCode"].ToString());
          var ttQuoteHedNew = quoteDS.QuoteHed.NewRow();
          BufferCopy.Copy(qHed, ttQuoteHedNew);
          quoteDS.QuoteHed.Add(ttQuoteHedNew);

//InfoMessage.Publish("After buffercopy " + qHed["SalesRepCode"].ToString());
      
          qHed["Number06"] = hedNum06TotalOrderCost;
          qHed["Number05"] = hedNum05OrigGross;
          decimal docTotalGrossValue = totalGross;
      
          if(hedNum05OrigGross != 0)  
          {
            decimal origPlusOrMinusPercent = hedNum05OrigGross == 0 ? 0 : (docTotalGrossValue - hedNum05OrigGross) / (hedNum05OrigGross) * 100;
            qHed["Number07"] = (origPlusOrMinusPercent);
          }
          else
          {
            qHed["Number07"] = 0.00;
          }
      
          if(hedNum06TotalOrderCost != 0)  
          {
            decimal originalGrossLessCatalogOriginalPrices = hedNum05OrigGross - totalCatalogOriginalPrices;
            decimal originalGMPercent = originalGrossLessCatalogOriginalPrices == 0 ? 0 : (100 - (totalOrderCostLessCatalogItems / originalGrossLessCatalogOriginalPrices) * 100);
            qHed["Number08"] = (originalGMPercent);
          }
          else
          {
            qHed["Number08"] = 0.00;
          }
      
          qHed["CheckBox04"] = false;

#endregion
  
#region 10
    
          qHed["RowMod"] = "U";

#endregion

      }
      
      long elapsedTicks2 = DateTime.Now.Ticks;
      TimeSpan elapsedSpan2 = new TimeSpan(elapsedTicks2);
      elapsedTicks2 = elapsedTicks2 - elapsedTicks;
      elapsedSpan2 = new TimeSpan(elapsedTicks2);
      //InfoMessage.Publish("Duration: " + Convert.ToDecimal(elapsedSpan2.TotalSeconds).ToString());

#region 11

foreach (var qq in quoteDS.QuoteQty)
{
    var qL = quoteDS.QuoteDtl.Where(x => x.QuoteLine == qq.QuoteLine && x.QuoteNum == qq.QuoteNum).FirstOrDefault();
    if(qL.DocExpUnitPrice != qq.UnitPrice || qL.DocExpUnitPrice != qq.DocUnitPrice)
    {
        qq.UnitPrice = qq.DocUnitPrice = qL.DocExpUnitPrice;
        qq.RowMod = "U";
    }
}

#endregion

#region 12
hQuote.Update(ref quoteDS);

#endregion

    }//End try
    catch (Exception e)
    {
        InfoMessage.Publish("BPM: " + e.Message);
    }//End catch
    finally
    {
        //Clear data
        callContextBpmData.Number01 = 0;
        hQuote = null;
    
        long elapsedTicks2 = DateTime.Now.Ticks;
        TimeSpan elapsedSpan2 = new TimeSpan(elapsedTicks2);
        elapsedTicks2 = elapsedTicks2 - elapsedTicks;
        elapsedSpan2 = new TimeSpan(elapsedTicks2);
       // InfoMessage.Publish("Duration Total: " + Convert.ToDecimal(elapsedSpan2.TotalSeconds).ToString());
      
        if (isLogging) InfoMessage.Publish(log.ToString());
    }

  }
  tx.Complete();
}