﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VAdvantage.Classes;
using VAdvantage.Common;
using VAdvantage.Process;
using VAdvantage.Model;
using VAdvantage.DataBase;
using VAdvantage.SqlExec;
using VAdvantage.Utility;
using System.Data;
using System.Data.SqlClient;
using VAdvantage.Logging;
using VAdvantage.ProcessEngine;
using ViennaAdvantage.Model;
using ViennaAdvantage.PaymentClass;



namespace ViennaAdvantage.Process
{
    public class VA009_CreateBatchLineForm : SvrProcess
    {
        int _docType = 0;
        int _C_invoice_ID = 0;
        int _C_BPartner_ID = 0;
        int _C_Bank_ID = 0;
        int _BPartner = 0;
        private string docBaseType = string.Empty;
        int _paySchedule_ID = 0;
        int _paymentMethod = 0;
        bool _trigger = false;
        DateTime? _DateDoc_From = null;
        DateTime? _DateDoc_To = null;
        int _VA009_BatchLine_ID = 0;
        int batchid = 0, _C_BankAccount_ID=0;
        bool VA009_IsSameCurrency = false;
        int C_ConversionType_ID = 0;
        //int _VA009_BatchDetail_ID = 0;


        protected override void Prepare()
        {
            ProcessInfoParameter[] para = GetParameter();
            for (int i = 0; i < para.Length; i++)
            {
                String name = para[i].GetParameterName();
                if (para[i].GetParameter() == null && para[i].GetParameter_To() == null)
                {
                    ;
                }
                else if (name.Equals("C_DocType_ID"))
                {
                    _docType = para[i].GetParameterAsInt();
                }
                else if (name.Equals("C_Invoice_ID"))
                {
                    _C_invoice_ID = para[i].GetParameterAsInt();
                }
                else if (name.Equals("C_BPartner_ID"))
                {
                    _C_BPartner_ID = para[i].GetParameterAsInt();
                }
                else if (name.Equals("C_Bank_ID"))
                {
                    _C_Bank_ID = para[i].GetParameterAsInt();
                }
                else if (name.Equals("C_BankAccount_ID"))
                {
                    _C_BankAccount_ID = para[i].GetParameterAsInt();
                }
                else if (name.Equals("C_InvoicePaySchedule_ID"))
                {
                    _paySchedule_ID = para[i].GetParameterAsInt();
                }
                else if (name.Equals("VA009_PaymentMethod_ID"))
                {
                    _paymentMethod = para[i].GetParameterAsInt();
                }
                else if (name.Equals("DateInvoiced"))
                {
                    _DateDoc_From = (DateTime?)(para[i].GetParameter());
                    _DateDoc_To = (DateTime?)(para[i].GetParameter_To());
                }
                else if (name.Equals("VA009_IsSameCurrency"))
                {
                    VA009_IsSameCurrency = "Y".Equals(para[i].GetParameter());
                }
                else if (name.Equals("C_ConversionType_ID"))
                {
                    C_ConversionType_ID = para[i].GetParameterAsInt();
                }
                else
                {
                    log.Log(Level.SEVERE, "Unknown Parameter: " + name);
                }
            }
        }

        protected override string DoIt()
        {
            batchid=GetBatchId();
            if (batchid > 0)
            {
                StringBuilder _sql = new StringBuilder();
                MVA009Batch batch = new MVA009Batch(GetCtx(), batchid, Get_TrxName());
                MBankAccount _bankacc = new MBankAccount(GetCtx(), batch.GetC_BankAccount_ID(), Get_TrxName());
                MVA009PaymentMethod _paymthd = null;
                MVA009BatchLines line = null;
                MVA009BatchLineDetails lineDetail = null;
                decimal dueamt = 0;
                _sql.Clear();
                _sql.Append(@"Select cp.ad_client_id, cp.ad_org_id,CI.C_Bpartner_ID, ci.c_invoice_id, cp.c_invoicepayschedule_id, cp.duedate, 
                              cp.dueamt, cp.discountdate, cp.discountamt,cp.va009_paymentmethod_id,ci.c_currency_id , doc.DocBaseType
                              From C_Invoice CI inner join C_InvoicePaySchedule CP ON CI.c_invoice_id= CP.c_invoice_id
                              INNER JOIN C_DocType doc ON doc.C_DocType_ID = CI.C_DocType_ID  Where ci.ispaid='N' AND cp.va009_ispaid='N' AND cp.C_Payment_ID IS NULL
                              AND CI.IsActive = 'Y' and ci.docstatus in ('CO','CL') AND cp.VA009_ExecutionStatus NOT IN ( 'Y','J') AND CI.AD_Client_ID = " + GetAD_Client_ID() + " AND CI.AD_Org_ID = " + GetAD_Org_ID());

                if (_C_BPartner_ID > 0)
                {
                    _sql.Append("  and CI.C_Bpartner_ID=" + _C_BPartner_ID);
                }
                if (_C_invoice_ID > 0)
                {
                    _sql.Append("  and CI.C_invoice_ID=" + _C_invoice_ID);
                }
                if (_paySchedule_ID > 0)
                {
                    _sql.Append(" AND CP.C_InvoicePaySchedule_ID=" + _paySchedule_ID);
                }
                if (_docType > 0)
                {
                    _sql.Append(" ANd CI.C_DocType_ID=" + _docType);
                }
                else
                {
                    _sql.Append(" ANd doc.DocBaseType IN ('API' , 'ARI' , 'APC' , 'ARC') ");
                }
                if (_paymentMethod > 0)
                {
                    _sql.Append(" And CP.VA009_PaymentMethod_ID=" + _paymentMethod);
                    _paymthd = new MVA009PaymentMethod(GetCtx(), _paymentMethod, Get_TrxName());
                    _trigger = _paymthd.IsVA009_IsMandate();
                }

                if (_DateDoc_From != null && _DateDoc_To != null)
                {
                    _sql.Append(" and cp.duedate BETWEEN  ");
                    _sql.Append(GlobalVariable.TO_DATE(_DateDoc_From, true) + " AND ");
                    _sql.Append(GlobalVariable.TO_DATE(_DateDoc_To, true));
                }
                else if (_DateDoc_From != null && _DateDoc_To == null)
                {
                    _sql.Append(" and cp.duedate >=" + GlobalVariable.TO_DATE(_DateDoc_From, true));
                }
                else if (_DateDoc_From == null && _DateDoc_To != null)
                {
                    _sql.Append(" and cp.duedate <=" + GlobalVariable.TO_DATE(_DateDoc_To, true));
                }
                else if (C_ConversionType_ID > 0)
                {
                    _sql.Append("  AND C_ConversionType_ID=" + C_ConversionType_ID);
                }

                if (VA009_IsSameCurrency == true)
                    _sql.Append(" AND CI.C_Currency_ID =" + _bankacc.GetC_Currency_ID());

                _sql.Append(" Order by CI.C_Bpartner_ID asc , doc.docbasetype ");

                DataSet ds = new DataSet();
                ds = DB.ExecuteDataset(_sql.ToString(), null, Get_TrxName());
                if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                {
                    for (int i = 0; i < ds.Tables[0].Rows.Count; i++)
                    {
                        if ((Util.GetValueOfDecimal(ds.Tables[0].Rows[i]["DueAmt"])) == 0)
                        {
                            continue;
                        }
                        // if invoice is of AP Invoice and AP Credit Memo then make a single Batch line
                        if (docBaseType == "API" || docBaseType == "APC")
                        {
                            if (_BPartner == Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_BPartner_ID"]) &&
                                ("API" == Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) || "APC" == Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"])))
                            {
                                line = new MVA009BatchLines(GetCtx(), _VA009_BatchLine_ID, Get_TrxName());
                            }
                            else
                            {
                                line = null;
                            }
                        }
                        // if invoice is of AR Invoice and AR Credit Memo then make a single Batch line
                        else if (docBaseType == "ARI" || docBaseType == "ARC")
                        {
                            if (_BPartner == Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_BPartner_ID"]) &&
                                ("ARI" == Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) || "ARC" == Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"])))
                            {
                                line = new MVA009BatchLines(GetCtx(), _VA009_BatchLine_ID, Get_TrxName());
                            }
                            else
                            {
                                line = null;
                            }
                        }
                        //if (_BPartner == Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_BPartner_ID"]) && docBaseType == Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]))
                        //{
                        //    line = new MVA009BatchLines(GetCtx(), _VA009_BatchLine_ID, null);
                        //}
                        // else
                        if (line == null)
                        {
                            line = new MVA009BatchLines(GetCtx(), 0, Get_TrxName());
                            line.SetAD_Client_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["Ad_Client_ID"]));
                            line.SetAD_Org_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["Ad_Org_ID"]));
                            line.SetVA009_Batch_ID(batch.GetVA009_Batch_ID());

                            _BPartner = Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_BPartner_ID"]);
                            docBaseType = Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]);

                            line.SetC_BPartner_ID(_BPartner);
                            if (_trigger == true)
                            {
                                _sql.Clear();
                                _sql.Append("Select VA009_BPMandate_id from C_BPartner Where C_BPartner_ID=" + _BPartner + " AND IsActive = 'Y' AND AD_Client_ID = " + GetAD_Client_ID());
                                DataSet ds1 = new DataSet();
                                ds1 = DB.ExecuteDataset(_sql.ToString(), null, Get_TrxName());
                                if (ds1.Tables != null && ds1.Tables.Count > 0 && ds1.Tables[0].Rows.Count > 0)
                                {
                                    line.SetVA009_BPMandate_ID(Util.GetValueOfInt(ds1.Tables[0].Rows[0]["VA009_BPMandate_id"]));
                                }
                            }
                            if (line.Save())
                            {
                                line.SetProcessed(true);
                                line.Save();
                                _VA009_BatchLine_ID = line.GetVA009_BatchLines_ID();
                            }
                            else
                            {
                                _BPartner = 0;
                                _VA009_BatchLine_ID = 0;
                            }
                        }
                        lineDetail = new MVA009BatchLineDetails(GetCtx(), 0, Get_TrxName());
                        lineDetail.SetAD_Client_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["Ad_Client_ID"]));
                        lineDetail.SetAD_Org_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["Ad_Org_ID"]));
                        lineDetail.SetVA009_BatchLines_ID(line.GetVA009_BatchLines_ID());
                        lineDetail.SetC_Invoice_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_Invoice_ID"]));
                        lineDetail.SetC_InvoicePaySchedule_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_InvoicePaySchedule_id"]));
                        lineDetail.SetDueDate(Util.GetValueOfDateTime(ds.Tables[0].Rows[i]["DueDate"]));
                        lineDetail.SetC_ConversionType_ID(C_ConversionType_ID);
                        dueamt = (Util.GetValueOfDecimal(ds.Tables[0].Rows[i]["DueAmt"]));
                        if (Util.GetValueOfDateTime(ds.Tables[0].Rows[i]["DiscountDate"]) >= Util.GetValueOfDateTime(batch.GetVA009_DocumentDate()))
                        {
                            dueamt = dueamt - (Util.GetValueOfDecimal(ds.Tables[0].Rows[i]["DiscountAmt"]));
                            //  145-2.88
                        }

                        bool issamme = true; decimal comvertedamt = 0;
                        if (Util.GetValueOfInt(ds.Tables[0].Rows[i]["c_currency_id"]) == _bankacc.GetC_Currency_ID())
                            issamme = true;
                        else
                            issamme = false;

                        if (Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) == "APC" || Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) == "ARC")
                        {
                            lineDetail.SetDueAmt(-1 * dueamt);
                            comvertedamt = (-1 * dueamt);
                        }
                        else
                        {
                            lineDetail.SetDueAmt(dueamt);
                            comvertedamt = (dueamt);
                        }
                        if (issamme == false)
                        {
                            comvertedamt = 0;
                            comvertedamt = MConversionRate.Convert(GetCtx(), dueamt, Util.GetValueOfInt(ds.Tables[0].Rows[i]["c_currency_id"]), _bankacc.GetC_Currency_ID(), DateTime.Now, C_ConversionType_ID, GetCtx().GetAD_Client_ID(), GetCtx().GetAD_Org_ID());
                            lineDetail.SetC_Currency_ID(_bankacc.GetC_Currency_ID());
                            if (Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) == "APC" || Util.GetValueOfString(ds.Tables[0].Rows[i]["DocBaseType"]) == "ARC")
                            {
                                comvertedamt = (-1 * comvertedamt);
                            }
                        }
                        else
                        {
                            lineDetail.SetC_Currency_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["c_currency_id"]));
                        }

                        lineDetail.SetVA009_ConvertedAmt(comvertedamt);
                        lineDetail.SetVA009_PaymentMethod_ID(Util.GetValueOfInt(ds.Tables[0].Rows[i]["va009_paymentmethod_id"]));
                        if (Util.GetValueOfDateTime(ds.Tables[0].Rows[i]["DiscountDate"]) < Util.GetValueOfDateTime(batch.GetVA009_DocumentDate()))
                        {
                            lineDetail.SetDiscountDate(null);
                            lineDetail.SetDiscountAmt(0);
                        }
                        else if (Util.GetValueOfDateTime(ds.Tables[0].Rows[i]["DiscountDate"]) >= Util.GetValueOfDateTime(batch.GetVA009_DocumentDate()))
                        {
                            lineDetail.SetDiscountDate(Util.GetValueOfDateTime(ds.Tables[0].Rows[i]["DiscountDate"]));
                            lineDetail.SetDiscountAmt(Util.GetValueOfDecimal(ds.Tables[0].Rows[i]["DiscountAmt"]));
                        }

                        if (!lineDetail.Save())
                        {
                            //return"BatchLine Not Saved"; 
                        }
                        else
                        {
                            lineDetail.SetProcessed(true);
                            MInvoicePaySchedule _invpay = new MInvoicePaySchedule(GetCtx(), Util.GetValueOfInt(ds.Tables[0].Rows[i]["C_InvoicePaySchedule_id"]), Get_TrxName());
                            _invpay.SetVA009_ExecutionStatus("Y");
                            _invpay.Save();
                            lineDetail.Save();
                        }
                    }
                    batch.SetVA009_GenerateLines("Y");
                    batch.SetProcessed(true);
                    batch.Save();
                    if (_paymentMethod != 0)
                    {
                        //_paymthd = new MVA009PaymentMethod(GetCtx(), _paymentMethod, Get_TrxName());
                        batch.SetVA009_PaymentMethod_ID(_paymentMethod);
                        batch.SetVA009_PaymentRule(_paymthd.GetVA009_PaymentRule());
                        batch.SetVA009_PaymentTrigger(_paymthd.GetVA009_PaymentTrigger());
                        batch.Save();
                        if (_paymthd.GetVA009_PaymentRule() == "M")
                        {
                            VA009_CreatePayments payment = new VA009_CreatePayments();
                            payment.DoIt(batch.GetVA009_Batch_ID(), GetCtx(), Get_TrxName(), 0);
                        }
                        else if (_paymthd.GetVA009_PaymentRule() == "E")
                        {
                            VA009_ICICI_Snorkel _Snrkl = new VA009_ICICI_Snorkel();
                            _Snrkl.GetMethod(batch.GetVA009_Batch_ID(), GetCtx(), Get_TrxName());
                        }
                    }
                    return Msg.GetMsg(GetCtx(), "VA009_BatchLineCrtd"); ;
                }
                else
                {
                    DB.ExecuteQuery("DELETE FROM VA009_Batch WHERE VA009_Batch_ID=" + batchid,null,Get_TrxName());

                    return Msg.GetMsg(GetCtx(), "VA009_BatchLineNotCrtd");
                }
            }
            else
            {
                DB.ExecuteQuery("DELETE FROM VA009_Batch WHERE VA009_Batch_ID=" + batchid,null,Get_TrxName());

                return Msg.GetMsg(GetCtx(), "VA009_BatchNotCrtd");
            }
        }

        public int GetBatchId() {
            MVA009PaymentMethod paym = new MVA009PaymentMethod(GetCtx(), _paymentMethod, Get_TrxName());
            MVA009Batch batch = new MVA009Batch(GetCtx(), 0, Get_TrxName());
            batch.SetAD_Client_ID(GetCtx().GetAD_Client_ID());
            batch.SetAD_Org_ID(GetCtx().GetAD_Org_ID());
            batch.SetC_Bank_ID(_C_Bank_ID);
            batch.SetC_BankAccount_ID(_C_BankAccount_ID);
            batch.SetVA009_PaymentMethod_ID(_paymentMethod);
            batch.SetVA009_PaymentRule(paym.GetVA009_PaymentRule());
            batch.SetVA009_PaymentTrigger(paym.GetVA009_PaymentTrigger());
            batch.SetVA009_DocumentDate(DateTime.Now);
            if (!batch.Save())
            {
                batchid = 0;
            }
            return batch.GetVA009_Batch_ID();
        }
    }
}