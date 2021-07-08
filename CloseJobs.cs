var LaborDataSet = new Erp.Tablesets.LaborTableset();
string vMessage = "";
Erp.Contracts.LaborSvcContract hLaborHandle = null;
if (hLaborHandle == null)
{
 hLaborHandle = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.LaborSvcContract>(Db);
}

var ttUD40_xRow = (from ttUD40_Row in Db.UD40
                   where ttUD40_Row.Key1 == "JOBCLOSE"
                   select ttUD40_Row).FirstOrDefault();
if (ttUD40_xRow != null)
{

    if (!ttUD40_xRow.CheckBox01)
    {

        string empID = ttUD40_xRow.ShortChar01;
        string jobnum = ttUD40_xRow.Key2;
        decimal timein = 1;
        decimal timeout = 1;
        decimal qty = ttUD40_xRow.Number01;
        int Oper = Convert.ToInt32(ttUD40_xRow.Number02);
        int assemby = 0;
        DateTime Date1 = DateTime.Today;
        hLaborHandle.GetNewLaborDtlNoHdr(ref LaborDataSet, empID, false, Date1, timein, Date1, timeout);
        hLaborHandle.DefaultJobNum(ref LaborDataSet, jobnum);
        hLaborHandle.DefaultAssemblySeq(ref LaborDataSet, assemby);
        hLaborHandle.DefaultOprSeq(ref LaborDataSet, Oper, out vMessage);
        hLaborHandle.DefaultLaborQty(ref LaborDataSet, qty, out vMessage);
        var ttLabor_xRow = (from ttLabor_Row in LaborDataSet.LaborDtl
                            where ttLabor_Row.Company == Session.CompanyID &&
                            ttLabor_Row.RowMod == "A"
                            select ttLabor_Row).FirstOrDefault();
        if (ttLabor_xRow != null)
        {

            ttLabor_xRow.ClockinTime = timein;
            ttLabor_xRow.ClockOutTime = timeout;
            ttLabor_xRow.OprSeq = Oper;
            ttLabor_xRow.OpComplete = true;
            ttLabor_xRow.Complete = true;
            ttLabor_xRow.LaborHrs = 0;
            ttLabor_xRow.TimeAutoSubmit = true;

        }

        hLaborHandle.Update(ref LaborDataSet);
        string cMessageText = "";

        var ttLabor_xyzRow = (from ttLabor_Row in LaborDataSet.LaborDtl
                              select ttLabor_Row).FirstOrDefault();
        if (ttLabor_xyzRow != null)
        {

            ttLabor_xyzRow.NewDifDateFlag = 1;

            //objWriter.WriteLine ("test4");
        }
        hLaborHandle.Update(ref LaborDataSet);
        var ttLabor_xyRow = (from ttLabor_Row in LaborDataSet.LaborDtl
                             select ttLabor_Row).FirstOrDefault();
        if (ttLabor_xyRow != null)
        {
            ttLabor_xyRow.RowMod = "U";
        }
        hLaborHandle.SubmitForApproval(ref LaborDataSet, false, out cMessageText);
        var ttLabor_xywRow = (from ttLabor_Row in LaborDataSet.LaborDtl
                              select ttLabor_Row).FirstOrDefault();
        if (ttLabor_xywRow != null)
        {
            ttLabor_xywRow.TimeStatus = "A";

        }
        hLaborHandle.Update(ref LaborDataSet);
    }
    Db.UD40.Delete(ttUD40_xRow);

    var jobSvc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobClosingSvcContract>(this.Db, true);
    Erp.Tablesets.JobClosingTableset jobDS = null;

    jobDS = new Erp.Tablesets.JobClosingTableset();

    string mes = string.Empty;

    jobSvc.GetNewJobClosing(ref jobDS);
    var jRow = jobDS.JobClosing.Find(x => x.RowMod == "A");
    if (jRow != null)
    {
        //InfoMessage.Publish("closing", Ice.Common.BusinessObjectMessageType.Information, Ice.Bpm.InfoMessageDisplayMode.Individual);
        bool reqUI = false;
        jRow.JobNum = ttUD40_xRow.Key2;
        jobSvc.OnChangeJobNum(ttUD40_xRow.Key2, ref jobDS, out mes);
        jRow.JobClosed = true;
        jRow.JobComplete = true;
        jRow.ClosedDate = System.DateTime.Today;
        jRow.BackFlush = true;
        jRow.QuantityContinue = 1;
        jobSvc.PreCloseJob(ref jobDS, out reqUI);
        jobSvc.CloseJob(ref jobDS, out mes);
    }

    jobSvc = null;
    jobDS = null;

}
var ttUD40_yRow = (from ttUD40_Row in Db.UD40
                   where ttUD40_Row.Key1 == "JOBCLOSE2"
                   select ttUD40_Row).FirstOrDefault();
if (ttUD40_yRow != null)
{
    bool flag = false;
    var jobp = (from JobProd_Row in Db.JobProd
                where JobProd_Row.Company == Session.CompanyID &&
                            JobProd_Row.JobNum == ttUD40_yRow.Key2
                select JobProd_Row).FirstOrDefault();
    if (jobp != null)
    {
        if (jobp.WarehouseCode.Length > 0)
        {
            if (jobp.ReceivedQty > jobp.ProdQty || jobp.ReceivedQty == jobp.ProdQty)
            {
                flag = true;
            }
        }
        else if (jobp.OrderNum > 0)
        {
            if (jobp.ShippedQty > jobp.ProdQty || jobp.ShippedQty == jobp.ProdQty)
            {
                flag = true;
            }
        }
    }
    if (flag)
    {
        Db.UD40.Delete(ttUD40_yRow);
        var jobSvc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobClosingSvcContract>(this.Db, true);
        Erp.Tablesets.JobClosingTableset jobDS = null;

        jobDS = new Erp.Tablesets.JobClosingTableset();

        string mes = string.Empty;

        jobSvc.GetNewJobClosing(ref jobDS);
        var jRow = jobDS.JobClosing.Find(x => x.RowMod == "A");
        if (jRow != null)
        {
            bool reqUI = false;
            jRow.JobNum = ttUD40_yRow.Key2;
            jobSvc.OnChangeJobNum(ttUD40_yRow.Key2, ref jobDS, out mes);
            jRow.JobClosed = true;
            jRow.JobComplete = true;
            jRow.ClosedDate = System.DateTime.Today;
            jRow.BackFlush = true;
            jRow.QuantityContinue = 1;
            jobSvc.PreCloseJob(ref jobDS, out reqUI);
            jobSvc.CloseJob(ref jobDS, out mes);
        }

        jobSvc = null;
        jobDS = null;
    }
}
		