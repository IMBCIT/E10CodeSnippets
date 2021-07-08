// Utility variables ============================================================
var stopwatch = new Stopwatch();
stopwatch.Start();
var isLoggingOn = false;
var log = new StringBuilder();

log.AppendLine(bpmTitle);
log.AppendLine("BO: SalesOrder");
log.AppendLine("DIAGNOSTICS: Please ignore. Thanks!").AppendLine();


var bpmTitle = "BPM: MasterUpdate.CreditHoldChecker";
var bpmSignature = "- " + bpmTitle;

// Enum Types ==============================================================
var TableTypeEnum = new { Hed = 0, Dtl = 1, Rel = 2 };

var OrderTypeEnum = new
{
    NonJb = 0,
    JbCust = 1,
    JbSetSt = 2,
    JbUnsetSt = 3,
};

// Queries ======================================================================
#region Query Methods

Func<dynamic> GetHed =
() =>
{ 
    int orderNum;
    int custNum;
    string company;
    string shipToNum;
    string termsCode;
    string omniPaymentType_ShortChar06;
    int tableType;
    decimal unaccountedForAmounts;
    bool isApprovedByCreditDepartment;

    if (ttOrderHed.Count() != 0)
    {
        if (ttOrderHed.Any(h => h.RowMod == "U" || h.RowMod == "A"))
        {
            var hed = ttOrderHed.First(h => h.RowMod == "U" || h.RowMod == "A");
            orderNum = hed.OrderNum;
            custNum = hed.CustNum;
            shipToNum = hed.ShipToNum;
            company = hed.Company;
            termsCode = hed.TermsCode;
            omniPaymentType_ShortChar06 = hed.UDField<string>("ShortChar06");
            tableType = TableTypeEnum.Hed;
            isApprovedByCreditDepartment = hed.UDField<bool>("CheckBox05");
        }
        else
        {
            return null;
        }
    }
    else if (ttOrderDtl.Count() != 0)
    {
        var dtl = ttOrderDtl.First(d => d.RowMod == "U" || d.RowMod == "A");
        var hed = Db.OrderHed.First(h => h.OrderNum == dtl.OrderNum && h.Company == dtl.Company);
        orderNum = hed.OrderNum;
        custNum = hed.CustNum;
        company = hed.Company;
        shipToNum = hed.ShipToNum;
        termsCode = hed.TermsCode;
        omniPaymentType_ShortChar06 = hed.UDField<string>("ShortChar06");
        tableType = TableTypeEnum.Dtl;
        isApprovedByCreditDepartment = hed.UDField<bool>("CheckBox05");
    }
    else if (ttOrderRel.Count() != 0)
    {
        var rel = ttOrderRel.First(r => r.RowMod == "U" || r.RowMod == "A");
        var hed = Db.OrderHed.First(h => h.OrderNum == rel.OrderNum && h.Company == rel.Company);
        orderNum = hed.OrderNum;
        custNum = hed.CustNum;
        company = hed.Company;
        shipToNum = hed.ShipToNum;
        termsCode = hed.TermsCode;
        omniPaymentType_ShortChar06 = hed.UDField<string>("ShortChar06");
        tableType = TableTypeEnum.Rel;
        isApprovedByCreditDepartment = hed.UDField<bool>("CheckBox05");
    }
    else
    {
        return null;
    }

    // Calculate unaccountedForAmounts.
    var totalChangedAmounts = ttOrderDtl
        .Where(d => d.RowMod == "A" || d.RowMod == "U").Select(d => d.DocTotalPrice)
        .DefaultIfEmpty(0).Sum();
    var totalOriginalAmounts = ttOrderDtl
        .Where(d => string.IsNullOrWhiteSpace(d.RowMod)).Select(d => d.DocTotalPrice)
        .DefaultIfEmpty(0).Sum();

    unaccountedForAmounts = totalChangedAmounts - totalOriginalAmounts;

    return new
    {
        Company = company,
        OrderNum = orderNum,
        CustNum = custNum,
        ShipToNum = shipToNum,
        TermsCode = termsCode,
        OmniPaymentType_ShortChar06 = omniPaymentType_ShortChar06,
        TableType = tableType,
        UnaccountedForAmounts = unaccountedForAmounts,
        IsApprovedByCreditDepartment = isApprovedByCreditDepartment
    };
};

Func<int, string, string, decimal> SumOfTheOpenStartUpInvoicesForShipTo =
(int custNum, string shipToNum, string company) =>
{
    var shipTo = Db.ShipTo.FirstOrDefault(s => s.Company == company && s.CustNum == custNum && s.ShipToNum == shipToNum);
    if (shipTo == null) return 0m;
    else return shipTo.Number02;
};

Func<int, string, decimal> SumOfOpenInvoices =
(int custNum, string company) =>
{
    var openInvoices =
      from hed in Db.InvcHead
      where
        hed.Company == company
        && hed.CustNum == custNum
        && hed.OpenInvoice
      select new
      {
          InvoiceNum = hed.InvoiceNum,
          Total = hed.InvoiceBal,
          Customer = hed.CustNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = openInvoices.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("Open Invoices Query (Record Count): {0}", openInvoices.Count());
    log.AppendLine();
    log.AppendLine("Open Invoices Query (Results):");
    foreach (var invoice in openInvoices)
    {
       log.AppendFormat("Invoice {0} Customer {1} Terms {2} Total {3}",
       invoice.InvoiceNum, invoice.Customer, invoice.Terms, invoice.Total);
       log.AppendLine();
    }
    log.AppendFormat("Sum of Open Invoices: {0}", sum);
    log.AppendLine();

    return sum;
};

Func<int, string, decimal> SumOfNonJbOpenOrders =
(int custNum, string company) =>
{
    var nonJbOpenOrders =
      from hed in Db.OrderHed
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new { dtl.Company, dtl.OrderNum } into dtls
      where
        hed.Company == company
          && hed.CustNum == custNum
          && hed.TermsCode != "JB"
          && ((hed.OpenOrder && !hed.VoidOrder)
                  || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt,
          Customer = hed.CustNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = nonJbOpenOrders.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("Non JB Open Orders Query (Record Count): {0}", nonJbOpenOrders.Count());
    log.AppendLine();
    log.AppendLine("Non JB Open Orders Query (Results):");
    foreach (var order in nonJbOpenOrders)
    {
        log.AppendFormat("Order {0} Customer {1} Terms {2} Total {3}",
			order.OrderNum, order.Customer, order.Terms, order.Total);
        log.AppendLine();
    }
    log.AppendFormat("Sum of Non JB Open Orders: {0}", sum);
    log.AppendLine();

    return sum;
};

// Adding additional test case for non Jb for ship to 08\13\20
Func<int, string, string, decimal> SumOfNonJbOpenOrdersForShipTo =
(int custNum, string shipToNum, string company) =>
{
    var nonJbOpenOrdersShipTo =
      from hed in Db.OrderHed
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new { dtl.Company, dtl.OrderNum } into dtls
      where
          hed.Company == company
          && hed.CustNum == custNum
          && hed.ShipToNum == shipToNum
          && hed.TermsCode != "JB"
		  && ((hed.OpenOrder && !hed.VoidOrder)
                  || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt,
          Customer = hed.CustNum, // for logging
          ShipTo = hed.ShipToNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = nonJbOpenOrdersShipTo.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("Non JB Open Orders Query (Record Count): {0}", nonJbOpenOrdersShipTo.Count());
    log.AppendLine();
    log.AppendLine("Non JB Open Orders Query (Results):");
    foreach (var order in nonJbOpenOrdersShipTo)
    {
        log.AppendFormat("Order {0} Customer {1} ShipTo {2} Terms {3} Total {4}",
            order.OrderNum, order.Customer, order.ShipTo, order.Terms, order.Total);
        log.AppendLine();
    }
    log.AppendFormat("Sum of Non JB Open Orders: {0}", sum);
    log.AppendLine();

    return sum;
};

Func<int, string, decimal> SumOfJbOpenOrders =
(int custNum, string company) =>
{
    var jbCtOpenOrders =
      from hed in Db.OrderHed
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new {dtl.Company, dtl.OrderNum } into dtls
      where
          hed.Company == company
          && hed.CustNum == custNum
          && hed.TermsCode == "JB"
          && ((hed.OpenOrder && !hed.VoidOrder)
                  || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt,
          Customer = hed.CustNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = jbCtOpenOrders.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    // Log:
    log.AppendFormat("JB Customer Open Orders Query (Record Count): {0}",
        jbCtOpenOrders.Count());
    log.AppendLine();
    log.AppendLine("JB Customer Open Orders Query (Results):");
    foreach (var order in jbCtOpenOrders)
    {
        log.AppendFormat("Order {0} Customer {1} Terms {2} Total {3}",
            order.OrderNum, order.Customer, order.Terms, order.Total);
        log.AppendLine();
    }
    log.AppendFormat("Sum of JB Customer Open Orders: {0}", sum);
    log.AppendLine();

    return sum;
};

Func<int, string, string, decimal> SumOfJbOrdersForShipTo =
(int custNum, string shipToNum, string company) =>
{
    // Get all the jb open or closed orders for this ship to.
    var orders =
      from hed in Db.OrderHed
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new {dtl.Company, dtl.OrderNum } into dtls
      where
      hed.Company == company
          && hed.CustNum == custNum
          && hed.ShipToNum == shipToNum
          && hed.TermsCode == "JB"
		  && ((hed.OpenOrder && !hed.VoidOrder)
                  || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt, //dtls.Select(d => d.ExtPriceDtl).DefaultIfEmpty(0).Sum(),
          Customer = hed.CustNum, // for logging
          ShipTo = hed.ShipToNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = orders.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("JB ShipTo Open/Closed Orders Query (Record Count): {0}", orders.Count());
    log.AppendLine();
    log.AppendLine("JB ShipTo Open/Closed Orders Query (Results):");
    foreach (var order in orders)
    {
        log.AppendFormat("Order {0} Customer {1} ShipTo {2} Terms {3} Total {4}",
            order.OrderNum, order.Customer, order.ShipTo, order.Terms, order.Total);
        log.AppendLine();
    }

    return sum;
};

Func<int, string, decimal> SumOfEveryShipToJbOpenOrderForCustomer =
(int custNum, string company) =>
{
    var orders =
      from hed in Db.OrderHed
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new {dtl.Company, dtl.OrderNum } into dtls
      where
          hed.Company == company
          && hed.CustNum == custNum
          && hed.ShipToNum != string.Empty
          && hed.TermsCode == "JB"
          && ((hed.OpenOrder && !hed.VoidOrder) || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt, //dtls.Select(d => d.ExtPriceDtl).DefaultIfEmpty(0).Sum(),
          Customer = hed.CustNum, // for logging
          ShipTo = hed.ShipToNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = orders.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("JB ShipTo Open/Closed Orders Query (Record Count): {0}",
        orders.Count());
    log.AppendLine();
    log.AppendLine("JB ShipTo Open/Closed Orders Query (Results):");
    foreach (var order in orders)
    {
        log.AppendFormat("Order {0} Customer {1} ShipTo {2} Terms {3} Total {4}",
            order.OrderNum, order.Customer, order.ShipTo, order.Terms, order.Total);
        log.AppendLine();
    }

    return sum;
};

Func<int, string, decimal> SumOfEveryShipToJbOpenOrderWithNoPreLienTypesForCustomer =
(int custNum, string company) =>
{
    // Note: ship to pre lien type is shortchar03
    var orders =
      from hed in Db.OrderHed
      join st in Db.ShipTo on new { hed.Company, hed.CustNum, hed.ShipToNum } equals new { st.Company, st.CustNum, st.ShipToNum }
      join dtl in Db.OrderDtl on new { hed.Company, hed.OrderNum } equals new {dtl.Company, dtl.OrderNum } into dtls
      where
          hed.Company == company
          && hed.CustNum == custNum
          && hed.ShipToNum != string.Empty
          && st.ShortChar03 == string.Empty
          && hed.TermsCode == "JB"
          && ((hed.OpenOrder && !hed.VoidOrder) || dtls.Any(d => d.OpenLine && !d.VoidLine))
      select new
      {
          OrderNum = hed.OrderNum,
          Total = hed.DocOrderAmt,
          Customer = hed.CustNum, // for logging
          ShipTo = hed.ShipToNum, // for logging
          Terms = hed.TermsCode, // for logging
      };

    var sum = orders.Select(o => o.Total).DefaultIfEmpty(0).Sum();

    log.AppendFormat("JB ShipTo Open/Closed Orders Query (Record Count): {0}",
        orders.Count());
    log.AppendLine();
    log.AppendLine("JB ShipTo Open/Closed Orders Query (Results):");
    foreach (var order in orders)
    {
        log.AppendFormat("Order {0} Customer {1} ShipTo {2} Terms {3} Total {4}",
            order.OrderNum, order.Customer, order.ShipTo, order.Terms, order.Total);
        log.AppendLine();
    }


    return sum;
};

#endregion

// Setters ======================================================================
#region Data Specific Methods

Func<int, string, int, bool> TrySetHedCheckBox01 =
(int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        hed["CheckBox01"] = true;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        hed.CheckBox01 = true;
    }

    return true;
};

Func<int, string, int, bool> TryClearHedCheckBox01 =
(int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        hed["CheckBox01"] = false;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        hed.CheckBox01 = false;
    }

    return true;
};

Func<string, int, string, int, bool> TrySetHedShortChar10 =
(string value, int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        hed["ShortChar10"] = value;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        hed.ShortChar10 = value;
    }

    return true;
};

Func<string, int, string, int, bool> TrySetHedCharacter01 =
(string value, int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        hed["Character01"] = value;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        hed.Character01 = value;
    }

    return true;
};

Func<string, int, string, int, bool> TryAppendToHedCharacter01 =
(string value, int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        var character01 = string.IsNullOrWhiteSpace(hed["Character01"].ToString()) ? string.Empty : hed["Character01"].ToString() + "\n";
        hed["Character01"] = character01 + value;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        var character01 = string.IsNullOrWhiteSpace(hed.Character01) ? string.Empty : hed.Character01 + "\n";
        hed.Character01 = character01 + value;
    }

    return true;
};

Func<int, string, int, bool> TryClearHedCheckBox05 =
(int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        hed.SetUDField("CheckBox05", false);
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        hed.CheckBox05 = false;
    }

    return true;
};

Func<string, int, string, bool> TrySetCustomerShortChar02 =
(string value, int custNum, string company) =>
{
    var customer = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum);
    if (customer == null) return false;

    if (customer.ShortChar02 != value)
        customer.ShortChar02 = value;

    return true;
};

// Data Comparisons =============================================================
Func<string, int, string, int, bool> IsHedShortChar10Equal =
(string value, int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        return hed["ShortChar10"] == value;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        return hed.ShortChar10 == value;
    }
};

Func<string, int, string, int, bool> IsHedCharacter01Equal =
(string value, int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        return hed["Character01"] == value;
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        return hed.Character01 == value;
    }
};

Func<int, string, int, bool> IsHedCharacter01Stamped =
(int orderNum, string company, int tableType) =>
{
    if (tableType == TableTypeEnum.Hed)
    {
        var hed = ttOrderHed.FirstOrDefault(h => h.RowMod == "U" || h.RowMod == "A");
        if (hed == null)
        {
            return false;
        }

        return !hed["Character01"].ToString().StartsWith("Credit");
    }
    else
    {
        var hed = Db.OrderHed.FirstOrDefault(h => h.Company == company && h.OrderNum == orderNum);
        if (hed == null)
        {
            return false;
        }

        return !hed.Character01.StartsWith("Credit");
    }
};

#endregion

// Business Methods =============================================================
#region Business Methods

Action<int, string, int, string> PutOnCreditHold =
(int orderNum, string company, int tableType, string msg) =>
{
    var sb = new StringBuilder();

    if (TrySetHedCheckBox01(orderNum, company, tableType))
    {
        // store the message
        if (!IsHedCharacter01Equal(msg, orderNum, company, tableType))
        {
            TrySetHedCharacter01(msg, orderNum, company, tableType);
            sb.AppendLine(msg).AppendLine().AppendLine(bpmSignature);

            InfoMessage.Publish(sb.ToString());
            //}
        }
    }
    else
    {
        sb.AppendLine("Unable to put this order on credit hold.");
        sb.AppendLine("Please contact support at itdepartment@mfgsoul.com.");
        sb.AppendLine();
        sb.AppendLine("Reason for trying: ");
        sb.AppendLine(msg);
        sb.AppendLine();
        sb.Append(bpmSignature);

        InfoMessage.Publish(sb.ToString());
        //}
    }
};

Action<int, string, int> TakeOffCreditHold =
(int orderNum, string company, int tableType) =>
{
    var sb = new StringBuilder();

    if (TryClearHedCheckBox01(orderNum, company, tableType))
    {
        TrySetHedCharacter01("Credit Check " + DateTime.Now.ToShortDateString(), orderNum, company, tableType);
    }
};

Action<int, string, string> SetCustomerCreditFlag =
(int custNum, string company, string value) =>
{
    var isSet = TrySetCustomerShortChar02(value, custNum, company);
    if (!isSet)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Unable to set this customer's customer flag.");
        sb.AppendLine().Append(bpmSignature);
        InfoMessage.Publish(sb.ToString());
    }
};

Action<int, string, int> Unapprove =
(int orderNum, string company, int tableType) =>
{
    var sb = new StringBuilder();
    var isCleared = TryClearHedCheckBox05(orderNum, company, tableType);
    if (isCleared)
    {
        sb.AppendLine("The order amount has increased and is no longer approved.");
        sb.AppendLine();
        sb.Append(bpmSignature);
        InfoMessage.Publish(sb.ToString());
    }

    if (!isCleared)
    {
        sb.AppendLine("Unable to set CheckBox05 (Approved by Credit Dpt) on the Header.");
        sb.AppendLine("Please contact support at itdepartment@mfgsoul.com.");
        sb.AppendLine();
        sb.Append(bpmSignature);
        InfoMessage.Publish(sb.ToString());
    }
};

Func<bool> IsOrderIncreased =
() =>
{
    var sumBeforeChanges = ttOrderDtl
        .Where(d => string.IsNullOrWhiteSpace(d.RowMod)).Select(d => d.DocExtPriceDtl)
        .DefaultIfEmpty(0).Sum();
    var sumAfterChanges = ttOrderDtl
        .Where(d => d.RowMod == "U" | d.RowMod == "A").Select(d => d.DocExtPriceDtl)
        .DefaultIfEmpty(0).Sum();

    return sumAfterChanges > sumBeforeChanges;
};

Func<string, bool>
IsShipToPreLienTypeSet = (string preLienType) =>
{
    return !string.IsNullOrWhiteSpace(preLienType);
};

#endregion

// Workflow Methods =============================================================
#region Workflow Methods

// checking is customer ship to is JB /08/13/20
/* Func<int, string, string, bool> IsShipToJB =
(int custNum, string shipToNum, string company) =>
{
  var shipToTerms =
    from st in Db.ShipTo
    where
      st.Company == company
      && st.CustNum == custNum
      && st.ShipToNum == shipToNum
      select new
      {
        Terms = st.ShortChar02,
      };
      
      var shipToJB = shipToTerms.Select(s => s.Terms);
      return (shipToJB.Equals("JB"));
};
*/

Func<string, bool> IsCashOrder =
(string terms) =>
{
    return (terms == "C24" || terms == "C22" || terms == "C20" || terms == "JBE");
};

Func<dynamic, bool> QualifyApprovalStatus =
(dynamic hed) =>
{
    var isOrderIncreased = IsOrderIncreased();
    if (hed.IsApprovedByCreditDepartment)
    {
        if (isOrderIncreased)
        {
            Unapprove(hed.OrderNum, hed.Company, hed.TableType); //DH Removed CustNum from this 6-11-2021
            return false;
        }
        else return true;
    }
    else return false;
};

Func<int, string, bool> IsCustomerOnHold =
(int custNum, string company) =>
{
    var customer = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum);
    return !string.IsNullOrWhiteSpace(customer.ShortChar02);
};

Func<int, string, string, bool> IsShipToOnHold =
(int custNum, string shipToNum, string company) =>
{
    log.AppendLine();
    log.AppendLine("Method: IsShipToOnHold");
    log.AppendLine("ShipToNum: " + shipToNum);
    log.AppendLine();

    if (string.IsNullOrWhiteSpace(shipToNum)) return false;

    var shipto = Db.ShipTo.FirstOrDefault(s => s.Company == company && s.CustNum == custNum && s.ShipToNum == shipToNum);
    return !string.IsNullOrWhiteSpace(shipto.Character01);
};

Func<int, string, string, string, int> GetOrderType =
(int custNum, string shipToNum, string company, string termsCode) =>
{
    var isShipToBlank = string.IsNullOrWhiteSpace(shipToNum);
    var isTermsJb = termsCode == "JB";
    var isNonJbOrder = !isTermsJb;
    var isJbCtOrder = isTermsJb && isShipToBlank;
    var isJbStOrder = isTermsJb && !isShipToBlank;

    int type = -1;

    if (isNonJbOrder)
        type = OrderTypeEnum.NonJb;
    else if (isJbCtOrder)
        type = OrderTypeEnum.JbCust;
    else if (isJbStOrder)
    {
        var shipTo = Db.ShipTo.FirstOrDefault(s => s.Company == company && s.CustNum == custNum && s.ShipToNum == shipToNum);
        var isStSet = IsShipToPreLienTypeSet(shipTo.ShortChar03);
        if (isStSet)
            type = OrderTypeEnum.JbSetSt;
        else // not set
            type = OrderTypeEnum.JbUnsetSt;
    }

    // Log

    log.AppendFormat("Is Non JB Order? {0}", type == OrderTypeEnum.NonJb);
    log.AppendLine();
    log.AppendFormat("Is JB Customer Order ? {0}", type == OrderTypeEnum.JbCust);
    log.AppendLine();
    log.AppendFormat("Is JB Set Ship To Order ? {0}", type == OrderTypeEnum.JbSetSt);
    log.AppendLine();
    log.AppendFormat("Is JB Set Ship To Order ? {0}", type == OrderTypeEnum.JbUnsetSt);
    log.AppendLine();
  
    return type;
};

#endregion

// 4 CASE Methods ===============================================================
#region CASE Methods

// CASE 1 Method ================================================================
Func<int, string, decimal, bool> IsNonJbOverLimit =
(int custNum, string company, decimal unaccountedForAmounts) =>
{
    var sum = SumOfNonJbOpenOrders(custNum, company) + SumOfOpenInvoices(custNum, company) + unaccountedForAmounts;

    var omniductCredit = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum).Number03;

    // Log:
    log.AppendFormat("Customer {0} omniduct credit: {1}", custId, customer.Number03);
    log.AppendLine();

    return sum > omniductCredit;
};

// CASE 2 Method ================================================================
Func<int, string, decimal, bool> IsJbCustOverLimit =
(int custNum, string company, decimal unaccountedForAmounts) =>
{
    // Sum up the total.
    var sum = SumOfJbOpenOrders(custNum, company) + SumOfOpenInvoices(custNum, company) + unaccountedForAmounts;

    // Check it against the customer unsecured [num02]
    var unsecuredCredit = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum).Number02;

    Log:
    log.AppendFormat("Customer {0} unsecured credit: {1}", custId, unsecuredCredit);
    log.AppendLine();

    return sum > unsecuredCredit;
};

// CASE 4 Methods ===============================================================
Func<int, string, decimal, bool> IsJbUnsetStOverCustomerLienLimit =
(int custNum, string company, decimal unaccountedForAmounts) =>
{
    var sum = SumOfEveryShipToJbOpenOrderForCustomer(custNum, company) + SumOfOpenInvoices(custNum, company) + unaccountedForAmounts;

    var lienThreshold = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum).Number01;

    return sum > lienThreshold;
};

Func<int, string, decimal, bool> IsJbUnsetStOverCustomerUnsecuredLimit =
(int custNum, string company, decimal unaccountedForAmounts) =>
{
    var sum = SumOfEveryShipToJbOpenOrderWithNoPreLienTypesForCustomer(custNum, company) + SumOfOpenInvoices(custNum, company) + unaccountedForAmounts;

    var unsecuredCredit = Db.Customer.FirstOrDefault(c => c.Company == company && c.CustNum == custNum).Number02;

    return sum > unsecuredCredit;
};

// Testing method for revisions per meeting with accounting 08/12/20
// CASE 3 Method ================================================================
Func<int, string, string, decimal, bool> IsJbSetStOverLimit =
(int custNum, string shipToNum, string company, decimal unaccountedForAmounts) =>
{
    var sum = SumOfJbOrdersForShipTo(custNum, shipToNum, company) 
	+ SumOfNonJbOpenOrdersForShipTo(custNum, shipToNum, company) 
	+ SumOfTheOpenStartUpInvoicesForShipTo(custNum, shipToNum, company) 
	+ unaccountedForAmounts;

    var shipToLienLimit = Db.ShipTo.FirstOrDefault(s => s.Company == company && s.CustNum == custNum && s.ShipToNum == shipToNum).Number01; // Job / ShipTo pre lien dollar amount 

    // Log: 
    log.AppendFormat("Sum of JB ShipTo Open/Closed Orders: {0}", sum);
    log.AppendLine();
    log.AppendFormat("ShipTo {0} lien dollar amount: {1}", shipToNum, shipToLienLimit);
    log.AppendLine();

    return sum > shipToLienLimit;
};

#endregion

// Main =========================================================================
#region Main


// Get this Hed. Exit if no Hed. ================================================
var thisHed = GetHed();
if (thisHed == null)
{
  return;
}

// Set Character 01, to let the Order know the Credit Check has occurred.
TryAppendToHedCharacter01("Credit Check " + DateTime.Now.ToString(), thisHed.OrderNum, thisHed.Company, thisHed.TableType);

log.AppendFormat("OrderNum: {0}", thisHed.OrderNum).AppendLine();
log.AppendFormat("TermsCode: {0}", thisHed.TermsCode).AppendLine();
log.AppendFormat("Customer: {0}", thisHed.CustNum);
log.AppendLine();


// Check for cash terms. Exit if terms are cash. ================================
if (IsCashOrder(thisHed.TermsCode))
{
  TakeOffCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType); 
  return;
}

// Check its approval status. Exit if already approved. =========================
if (QualifyApprovalStatus(thisHed)) return;

// Check if customer is on hold. Put on hold and exit. ==========================
if (IsCustomerOnHold(thisHed.CustNum, thisHed.Company))
{
  var msg = "The customer is on credit hold.";
  PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
  return;
}

// Check if ship to is on hold. Put on hold and exit. ===========================
if (IsShipToOnHold(thisHed.CustNum, thisHed.ShipToNum, thisHed.Company))
{
  var msg = "The ship to is on credit hold.";
  PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
  return;
}

// Get the type of order and check limit based on type. =========================
var orderType = GetOrderType(thisHed.CustNum, thisHed.ShipToNum, thisHed.Company, thisHed.TermsCode);


// CASE 1: Non Job Basis (Jb) Order =============================================
if (orderType == OrderTypeEnum.NonJb)
{
  if (IsNonJbOverLimit(thisHed.CustNum, thisHed.Company, thisHed.UnaccountedForAmounts))
  {
    var msg = "Customer exceeds credit limit.";
    PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
    // SetCustomerCreditFlag(thisHed.CustomerCustID, "(F)Flag"); 
  }
}

// CASE 2: Job Basis (Jb) Customer Order (No Ship To)============================
else if (orderType == OrderTypeEnum.JbCust)
{
  if (IsJbCustOverLimit(thisHed.CustNum, thisHed.Company, thisHed.UnaccountedForAmounts))
  {
    var msg = "Customer exceeds credit limit.";
    PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
    // SetCustomerCreditFlag(thisHed.CustomerCustID, "(F)Flag");
  }
}

// CASE 4: Job Basis (Jb) Unset Ship To Order ===================================
else if (orderType == OrderTypeEnum.JbUnsetSt)
{
  var isOverCustomerLienLimit = IsJbUnsetStOverCustomerLienLimit(thisHed.CustNum, thisHed.Company, thisHed.UnaccountedForAmounts);
  var isOverUnsecuredLimit = IsJbUnsetStOverCustomerUnsecuredLimit(thisHed.CustNum, thisHed.Company, thisHed.UnaccountedForAmounts);
  var msg = "";

  if (isOverUnsecuredLimit)
  {
  if (isOverCustomerLienLimit)
  {
    msg += "Warning: Customer exceeds Lien Limit threshold.\n";
  }
    msg += "Order puts customer over unsecured limit.";
  PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
    // SetCustomerCreditFlag(thisHed.CustomerCustID, "(F)Flag");
  }
  else if (isOverCustomerLienLimit)
  {
  msg += "Warning: Customer exceeds Lien Limit threshold.\n";
  var sb = new StringBuilder();
  sb.AppendLine(msg).AppendLine().AppendLine(bpmSignature);
  
  TrySetHedCharacter01(msg, thisHed.OrderNum, thisHed.Company, thisHed.TableType);
  
  InfoMessage.Publish(sb.ToString());
  }
}

// CASE 3: Job Basis (Jb) Set Ship To Order =====================================
else if (orderType == OrderTypeEnum.JbSetSt)
{
  if (IsJbSetStOverLimit(thisHed.CustNum, thisHed.ShipToNum, thisHed.Company, thisHed.UnaccountedForAmounts))
  {
    var msg = "Customer exceeds pre-lien dollar amount.";
    PutOnCreditHold(thisHed.OrderNum, thisHed.Company, thisHed.TableType, msg);
    // SetCustomerCreditFlag(thisHed.CustomerCustID, "(F)Flag");
  }
}

#endregion

// Display testing log===========================================================

stopwatch.Stop();
log.AppendLine();
log.AppendFormat("Total BPM Execution Time: {0:hh\\:mm\\:ss}*", stopwatch.Elapsed);
log.AppendLine();
log.AppendLine();
log.AppendLine("*Includes time user takes to read messages." );
if (isLoggingOn) { InfoMessage.Publish(log.ToString()) };

if (Session.UserID.ToLower() == "epicor.one")
{
  InfoMessage.Publish(log.ToString());
}