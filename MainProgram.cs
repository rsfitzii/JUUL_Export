using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Net.Sockets;

namespace JUUL_Export
{
    class MainProgram
    {
        static void Main(string[] args)
        {
            // args[0] = Run Mode
            // args[1] = StartDate (yyyy-mm-dd)
            // args[2] = EndDate (yyyy-mm-dd)


            MPCLoyaltyEntities db = new MPCLoyaltyEntities();

            DateTime StartDate_DT;
            DateTime EndDate_DT;
            DateTime InvEndDate_DT;
            string StartDate;
            string EndDate;
            string InvEndDate;
            string RunMode;

            if (args != null && args.Count() > 0 && args[0] != null)
                RunMode = args[0];
            else
                RunMode = "T";

            if (args != null && args.Count() > 1 && args[1] != null)
            {
                StartDate = args[1];
                EndDate = args[2];
                EndDate_DT = DateTime.Parse(EndDate);
                InvEndDate_DT = EndDate_DT.AddDays(-1);
                InvEndDate = InvEndDate_DT.ToString("yyyy-MM-dd");
            }
            else
            {
                DateTime today = DateTime.Today;

                if (today.DayOfWeek != DayOfWeek.Saturday)
                {
                    throw new Exception("Today is not saturday and no specific date range was provided.  Job cannot run.");
                }
                else
                {
                    StartDate_DT = today.AddDays(-7);
                    EndDate_DT = today.AddDays(-1);
                }

                /*
                if (today.DayOfWeek == DayOfWeek.Sunday)
                    StartDate_DT = today.AddDays(-1);
                else if (today.DayOfWeek == DayOfWeek.Monday)
                    StartDate_DT = today.AddDays(-2);
                else if (today.DayOfWeek == DayOfWeek.Saturday)
                    StartDate_DT = today.AddDays(-7);
                else
                    StartDate_DT = today;

                // now walk backwards until you find the second Saturday, that will be your starting Friday to run for
                while (StartDate_DT.DayOfWeek != DayOfWeek.Saturday)
                    StartDate_DT = StartDate_DT.AddDays(-1);
                EndDate_DT = StartDate_DT.AddDays(-1);
                StartDate_DT = StartDate_DT.AddDays(-7);
                
                
                while (StartDate_DT.DayOfWeek != DayOfWeek.Saturday)
                    StartDate_DT = StartDate_DT.AddDays(-1);

                EndDate_DT = StartDate_DT.AddDays(6);
                InvEndDate_DT = EndDate_DT.AddDays(-1);
                */

                InvEndDate_DT = EndDate_DT.AddDays(-1);

                StartDate = StartDate_DT.ToString("yyyy-MM-dd");
                EndDate = EndDate_DT.ToString("yyyy-MM-dd");
                InvEndDate = InvEndDate_DT.ToString("yyyy-MM-dd");
            }


            Console.WriteLine("Run Mode = " + RunMode);
            Console.WriteLine("Start Date = " + StartDate);
            Console.WriteLine("End Date = " + EndDate);
            Console.WriteLine("Inv Date = " + InvEndDate);

            string filename = "17003865_TOB_purchase_" + GetNumbers(EndDate) +".CSV";
            string dir = "\\\\admin01\\ITShare\\SSISTemp\\JUUL\\";
            string subdir = "Purchase\\";
            string fullPath = dir + subdir + filename;

            Directory.CreateDirectory(dir + subdir);

            List<JuulPurchaseFileRecord> theseRecords = new List<JuulPurchaseFileRecord>();

            string sql = "EXEC [dbo].[MPC_JUUL_MSA_Purch_Export_SP] " +
                         "@StartDate = '" + StartDate + "', " +
                         "@EndDate = '" + EndDate + "' ";
            theseRecords = db.Database.SqlQuery<JuulPurchaseFileRecord>(sql).DefaultIfEmpty().ToList();

            string outputLine = "";

            if (theseRecords != null && theseRecords.Count() > 0 && theseRecords[0] != null)
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                using (StreamWriter writer = new StreamWriter(fullPath))
                {
                    outputLine = '"' + "#ALT01#" + '"' + "," +
                                 '"' + "17003865" + '"' + "," +
                                 '"' + "TO" + '"' + "," +
                                 '"' + GetNumbers(EndDate) + '"' + "," +
                                 '"' + RunMode + '"' + "," +
                                 '"' + "02" + '"' + "," +
                                 '"' + "PURCHASE" + '"' + "," +
                                 '"' + '"';
                    writer.WriteLine(outputLine);

                    foreach (JuulPurchaseFileRecord thisRecord in theseRecords)
                    {
                        if (thisRecord.QuantityShipped > 0 && thisRecord.ShipToCustNum != "9008")
                        {
                            /*                            if (thisRecord.ItemDescription.Contains("NJOY"))
                                                            if (thisRecord.Units == 20 || thisRecord.Units == 10)
                                                                thisRecord.Units = 5;
                            */
                            outputLine = '"' + thisRecord.SKU + '"' + "," +
                                         '"' + thisRecord.ItemDescription + '"' + "," +
                                         '"' + thisRecord.Units + '"' + "," +
                                         '"' + thisRecord.ShipToCustNum + '"' + "," +
                                         '"' + thisRecord.ShipToCustName + '"' + "," +
                                         '"' + thisRecord.Address + '"' + "," +
                                         '"' + thisRecord.City + '"' + "," +
                                         '"' + thisRecord.State + '"' + "," +
                                         '"' + thisRecord.Zipcode.Trim() + '"' + "," +
                                         '"' + thisRecord.QuantityShipped + '"' + "," +
                                         '"' + thisRecord.CategoryCode + '"' + "," +
                                         '"' + thisRecord.PromoDescription + '"' + "," +
                                         '"' + "N" + '"' + "," +
                                         '"' + thisRecord.UPC + '"' + "," +
                                         '"' + "Retailer" + '"' + "," +
                                         '"' + thisRecord.State + '"' + "," +
                                         '"' + "0" + '"' + "," + // saleable returns (altria only)
                                         '"' + "0" + '"' + "," + // unsaleable returns (altria only)
                                         '"' + thisRecord.BusinessDate.ToString("yyyyMMdd") + '"'; // transaction date (altria only)
                            writer.WriteLine(outputLine);
                        }
                    }
                }

                TransferFile(dir + subdir, filename, RunMode);

            }

            sql = "select SKU,ItemDescription,Units,ShipToCustNumber,Address,City,State,Zipcode,sum(WhInv_End_Qty) as QuantityShipped,CategoryCode,PromoCodeDescription,PromoIndicator,UPC,ClassOfTrade,StateTaxJurisdiction " +
                  "from " +
                  "(" +
                     "select p.Prod_ID as SKU, p.Prod_Description as ItemDescription " +
                     ",1 as Units " +
                     ",s.Site_ID as ShipToCustNumber, s.Site_Address1 as Address, s.Site_City as City " +
                     ",o.[PB State_desc] as State " +
                      ",s.Site_Zip as Zipcode " +
                      ",case when p.Prod_Description like '%NJOY%' and pp.ProdPkg_Pack_Size = 10 then WhInv_End_Qty *50 " +
                          "else " +
                             "case when p.Prod_Description like '%NJOY%' and pp.ProdPkg_Pack_Size = 20 then WhInv_End_Qty *100 " +
                                  "else " +
                                    "WhInv_End_Qty " +
                            "end " +
                      "end as WhInv_End_Qty " +
                      ",'003292' as CategoryCode " +
                      ",'' as PromoCodeDescription " +
                      ",'' as PromoIndicator " +
                      ",pp.ProdPkg_UPC_Code as UPC " +
                      ",case when p.Prod_Description like '%NJOY%' then 'Distributor' else '' end as ClassOfTrade " +
                      ",case when p.Prod_Description like '%NJOY%' then o.[PB State_desc] else '' end as StateTaxJurisdiction " +
                      "from [C1065-01.mwpetro.com].[PDICompany_1065_01].[dbo].[Warehouse_Daily_Inventory] wdi " +
                      "join [C1065-01.mwpetro.com].[PDICompany_1065_01].[dbo].[Product_Packages] pp on pp.ProdPkg_Key = wdi.WhInv_ProdPkg_Key " +
                      "join [C1065-01.mwpetro.com].[PDICompany_1065_01].[dbo].[Products] p on p.Prod_Key = pp.ProdPkg_Prod_Key " +
                      "join [C1065-01.mwpetro.com].[PDICompany_1065_01].[dbo].[Sites] s on s.Site_Key = wdi.WhInv_Site_Key " +
                      "join [C1065-01.mwpetro.com].[PDI_Warehouse_1065_01].dbo.Organization o on s.Site_ID = o.Location_ID " +
                      "where wdi.WhInv_Date = '" + InvEndDate + "' and(p.Prod_Description like '%JUUL%' or p.Prod_Description like '%NJOY%') " +
                      "and pp.ProdPkg_Purchased < 0 " +
                    ") tmp " +
                      "group by SKU,ItemDescription,ShipToCustNumber,Address,City,State,Zipcode,CategoryCode,PromoCodeDescription,PromoIndicator,UPC,Units,ClassOfTrade,StateTaxJurisdiction " +
                      "order by ItemDescription ";
            Console.WriteLine(sql);

            theseRecords = db.Database.SqlQuery<JuulPurchaseFileRecord>(sql).DefaultIfEmpty().ToList();

            filename = "17003865_TOB_inventory_" + GetNumbers(EndDate) + ".CSV";
            subdir = "Inventory\\";
            fullPath = dir + subdir + filename;

            Directory.CreateDirectory(dir + subdir);

            if (theseRecords != null && theseRecords.Count() > 0 && theseRecords[0] != null)
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                using (StreamWriter writer = new StreamWriter(fullPath))
                {
                    outputLine = '"' + "#ALT01#" + '"' + "," +
                                 '"' + "17003865" + '"' + "," +
                                 '"' + "TO" + '"' + "," +
                                 '"' + GetNumbers(EndDate) + '"' + "," +
                                 '"' + RunMode + '"' + "," +
                                 '"' + "02" + '"' + "," +
                                 '"' + "INVENTORY" + '"' + "," +
                                 '"' + '"';
                    writer.WriteLine(outputLine);

                    foreach (JuulPurchaseFileRecord thisRecord in theseRecords)
                    {
                        if (thisRecord.QuantityShipped > 0)
                        {
                            outputLine = '"' + thisRecord.SKU + '"' + "," +
                                         '"' + thisRecord.ItemDescription + '"' + "," +
                                         '"' + thisRecord.Units + '"' + "," +
                                         /*
                                            '"' + thisRecord.ShipToCustNum + '"' + "," +
                                            '"' + thisRecord.ShipToCustName + '"' + "," +
                                            '"' + thisRecord.Address + '"' + "," +
                                            '"' + thisRecord.City + '"' + "," +
                                            '"' + thisRecord.State + '"' + "," +
                                            '"' + thisRecord.Zipcode.Trim() + '"' + "," +
                                         */
                                         '"' + thisRecord.QuantityShipped + '"' + "," +
                                         '"' + thisRecord.CategoryCode + '"' + "," +
                                         '"' + thisRecord.PromoDescription + '"' + "," +
                                         '"' + "N" + '"' + "," +
                                         '"' + '"' + "," +
                                         '"' + thisRecord.UPC + '"' + "," +
                                         '"' + "0" + '"'; // floor returns (altria only)
                                        /*
                                           '"' + thisRecord.ClassOfTrade + '"' + "," +
                                           '"' + thisRecord.StateTaxJurisdiction + '"';
                                        */
                            writer.WriteLine(outputLine);
                        }
                    }
                }
                TransferFile(dir + subdir, filename, RunMode);
            }
        }

        private static string GetNumbers(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        public static bool TransferFile(string path,string filename,string runMode)
        {
            bool ret = true;
            //return ret;

            string user = "17003865";
            string pass = "Pass456*";

            SftpClient client = new SftpClient("mfthub.msa.com", 22, user, pass);

Console.WriteLine($"Transferring path: {path}");
Console.WriteLine($"Transferring file: {filename}");

            string modeLit = "";
            if (runMode == "T")
                modeLit = "test";
            else
                modeLit = "live";

            try
            {
                client.Connect();
                if (client.IsConnected)
                {
                    string output = user + "_" + modeLit + "/incoming/" + filename;
                    client.UploadFile(File.OpenRead(path + "/" + filename), output);
                    client.Disconnect();
                }
            }
            catch (Exception e) when (e is SshConnectionException || e is SocketException || e is ProxyException)
            {
                Console.WriteLine($"Error connecting to server: {e.Message}");
                ret = false;
            }
            catch (SshAuthenticationException e)
            {
                Console.WriteLine($"Failed to authenticate: {e.Message}");
                ret = false;
            }
            catch (SftpPermissionDeniedException e)
            {
                Console.WriteLine($"Operation denied by the server: {e.Message}");
                ret = false;
            }
            catch (SshException e)
            {
                Console.WriteLine($"Sftp Error: {e.Message}");
                ret = false;
            }
            return ret;
        }

    }

    public class JuulPurchaseFileRecord
    {
        public string SKU { get; set; }
        public string ItemDescription { get; set; }
        public int Units { get; set; }
        public string ShipToCustNum { get; set; }
        public string ShipToCustName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set;  }
        public string Zipcode { get; set;  }
        public decimal QuantityShipped { get; set; }
        public string CategoryCode { get; set; }
        public string PromoDescription { get; set; }
        public string PromoIndicator { get; set; }
        public string UPC { get; set; }
        public string ClassOfTrade { get; set;  }
        public string StateTaxJurisdiction { get; set; }
        public DateTime BusinessDate { get; set; }

    }
}
