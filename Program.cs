using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Net.NetworkInformation;
using System.IO;
using System.Runtime.CompilerServices;

namespace SQLStreamable
{
    class Program
    {
        public static SqlConnection sql = new SqlConnection();


        public static ArrayList loaded = new ArrayList();
        public static ArrayList wrongformat = new ArrayList();
        public static ArrayList wrongconfigs = new ArrayList();
        public static ArrayList burl = new ArrayList();
        public static ArrayList bsource = new ArrayList();
        public static int totallinks = 0;
        public static int brokenlinks = 0;
        public static bool rewrite = false;

        public static string source = "";//@"Data Source=KyrarycPC;Initial Catalog=Streamable;Integrated Security=SSPI;";
        public static int minviews = -1;
        public static int maxviews = 10000;
        public static string selectnum = "";
        public static bool ensurenewview = false;
        public static int waittime = 0;
        static void Main(string[] args)
        {
            try
            {                
                if (!LoadConfig())
                {
                    return;
                }
                CheckRTs();
                CheckFiles();
                CheckLinks();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception encountered:  " + ex.Message + ", " + ex.StackTrace);
            }
            finally
            {
                if (sql.State == ConnectionState.Open)
                {
                    sql.Close();
                }
                Report();
            }
        }

        public static void CheckRTs()
        {
            if (!File.Exists("RTLinks.txt"))
            {
                return;
            }

            string[] rts = File.ReadAllLines("RTLinks.txt");
            if (rts.Length == 0)
            {
                return;
            }

            IWebDriver driver = new ChromeDriver();
            for (int i = 0; i < rts.Length; i++)
            {
                driver.Url = rts[i];
                string filename = driver.Title;
                
                IList alllinks = driver.FindElements(By.TagName("a"));

                for (int c = 0; c < alllinks.Count; c++)
                {
                    IWebElement curlink = (IWebElement) alllinks[c];
                    string link = curlink.GetAttribute("href");

                    if (link == null || !link.Contains("streamable.com"))
                    {
                        continue;
                    }

                    //Get link from database
                    string query = "Select * FROM [Streamable].[dbo].[Links] " +
                        " where link like '%" + link + "%'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = query,
                        CommandTimeout = 10
                    };

                    var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                    DataTable dt = new DataTable();
                    sqlAdapt.Fill(dt);

                    bool found = false;
                    for (int s = 0; s < dt.Rows.Count; s++)
                    {
                        string sqllink = dt.Rows[s]["link"].ToString();
                        if (sqllink.Equals(link))
                        {
                            string source = dt.Rows[s]["source"].ToString();
                            if (string.IsNullOrEmpty(source))
                            {//Double check that the source is stored
                                string sqlid = dt.Rows[s]["id"].ToString();
                                string update = "Update [Streamable].[dbo].[Links] set source = '" + filename + "' where id = " + sqlid;

                                var sqlUpdate = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = update,
                                    CommandTimeout = 10
                                };
                                sqlUpdate.ExecuteNonQuery();
                            }
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        //Wasn't found in sql, Insert it into the database
                        string insert = "insert into [Streamable].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                            "('" + link + "', '" + filename + "', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";

                        var sqlProcedureInsert = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = insert,
                            CommandTimeout = 10
                        };
                        sqlProcedureInsert.ExecuteNonQuery();
                    }
                }
                loaded.Add(filename);
            }
            StreamWriter sw = new StreamWriter("RTLinks.txt");
            sw.WriteLine("");
            driver.Close();
        }
        public static bool LoadConfig()
        {
            if (!File.Exists("Config.txt"))
            {
                return false;
            }

            string[] config = File.ReadAllLines("Config.txt");

            for (int i = 0; i < config.Length; i++)
            {
                string type = config[i].Substring(0, 7);
                string set = config[i].Substring(9);
                switch (type.ToUpper())
                {
                    case "CONNECT":
                        {
                            source = set;
                            try
                            {
                                sql.ConnectionString = source;
                                sql.Open();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Connection string not valid.  Ensure SQL Server is downloaded and the string is accurate.");
                                Console.WriteLine("My first time connection string should look like:  " + @"Data Source=KyrarycPC;Integrated Security=SSPI;");
                                Console.WriteLine("After setup it look like: " + @"Data Source=KyrarycPC;Integrated Security=SSPI;Initial Catalog=Streamable;");
                                Console.WriteLine("Replace 'KyrarycPC' with the name of your computer");

                                return false;
                            }
                            string testquery = "SELECT TOP 1 * FROM [Streamable].[dbo].[Links]";
                            try
                            {
                                var sqlProcedure = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = testquery,
                                    CommandTimeout = 10
                                };

                                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                                DataTable dt = new DataTable();
                                sqlAdapt.Fill(dt);

                            }
                            catch (Exception ex)
                            {
                                //Check if the STREAMABLE database exists
                                string createquery = "Create Database Streamable";
                                try
                                {
                                    var sqlProcedureDatabase = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureDatabase.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already exist"))
                                    {
                                        Console.WriteLine("Unable to create database.  Please open SQL Server and create a Streamable database");
                                        return false;
                                    }
                                }
                                if (!source.Contains("Initial Catalog"))
                                {
                                    source = source + "Initial Catalog=Streamable;";
                                    sql.Close();
                                    sql.ConnectionString = source;
                                    sql.Open();
                                    rewrite = true;
                                }
                                //Attempt to create the Link database
                                createquery = "CREATE TABLE[dbo].[Links](" +
                                    " [id][int] IDENTITY(1, 1) NOT NULL, [link] [text] NULL, [source] [text] NULL, [views] [int] NULL, [lastviewd] [date] NULL," +
                                    " CONSTRAINT[PK_Links] PRIMARY KEY CLUSTERED ([id] ASC )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                                try
                                {
                                    var sqlProcedureLink = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureLink.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already an object named"))
                                    {
                                        Console.WriteLine("Unable to add the links database.  Please open SQL Server and run the following query");
                                        Console.WriteLine(createquery);
                                        return false;
                                    }
                                }

                        //        string testinsert = "insert into [Test].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                        //"('Test', 'TEST1', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";
                        //        var sqlProcedureTest = new SqlCommand
                        //        {
                        //            Connection = sql,
                        //            CommandType = CommandType.Text,
                        //            CommandText = testinsert,
                        //            CommandTimeout = 10
                        //        };
                        //        sqlProcedureTest.ExecuteNonQuery();
                        //        sqlProcedureTest.ExecuteNonQuery();
                        //        sqlProcedureTest.ExecuteNonQuery();

                                //Attempt to create the broken database
                                createquery = "CREATE TABLE[dbo].[Broken](" +
                                    "[id][int] IDENTITY(1, 1) NOT NULL, [link] [text] NULL, [source] [text] NULL, [timestamp] [datetime] NULL," +
                                    "CONSTRAINT[PK_Broken] PRIMARY KEY CLUSTERED ([id] ASC)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                                try
                                {
                                    var sqlProcedureBroken = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureBroken.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already an object named"))
                                    {
                                        Console.WriteLine("Unable to add the broken links database.  Please open SQL Server and run the following query");
                                        Console.WriteLine(createquery);
                                        return false;
                                    }
                                }
                            }
                            break;
                        }
                    case "MINVIEW":
                        {
                            int view;
                            if (int.TryParse(set, out view))
                            {
                                minviews = view;
                            }
                            break;
                        }
                    case "MAXVIEW":
                        {
                            int view;
                            if (int.TryParse(set, out view))
                            {
                                maxviews = view;
                            }
                            break;
                        }
                    case "SELECT#":
                        {
                            int view;
                            if (!int.TryParse(set, out view))
                            {
                                selectnum = " * ";
                            }
                            else
                            {
                                selectnum = " Top " + view + " * ";
                            }
                            break;
                        }
                    case "WAITMIL":
                        {
                            int num;
                            if (!int.TryParse(set, out num))
                            {
                                waittime = 1500;
                            }
                            else
                            {
                                waittime = num;
                            }
                            break;
                        }
                    case "NEWVIEW":
                        {
                            if (set.ToUpper().Equals("TRUE") || set.ToUpper().Equals("YES"))
                            {
                                ensurenewview = true;
                            }
                            else
                            {
                                ensurenewview = false;
                            }
                            break;
                        }
                    default:
                        {
                            wrongconfigs.Add(type);
                            break;
                        }
                }
            }
            return true;
        }

        public static void Report()
        {

            if (wrongconfigs.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Config commands not recognized~~~~~~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < wrongconfigs.Count; i++)
                {
                    Console.WriteLine(wrongconfigs[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Config commands not recognized~~~~~~~~~~~~~~~~~~~~~~~~");
            }
            if (loaded.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Loaded Files~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < loaded.Count; i++)
                {
                    Console.WriteLine(loaded[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Loaded Files~~~~~~~~~~~~~~~~~~~");
            }
            if (wrongformat.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Improperly formatted files - .txt ONLY~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < wrongformat.Count; i++)
                {
                    Console.WriteLine(wrongformat[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Improperly formatted files - .txt ONLY~~~~~~~~~~~~~~~~~~~");
            }
            if (burl.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Broken Links~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < burl.Count; i++)
                {
                    Console.WriteLine(burl[i].ToString() + " - " + bsource[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Broken Links~~~~~~~~~~~~~~~~~~~");
            }
            if (rewrite)
            {
                Console.WriteLine("Rewriting config file");
                StreamWriter sw = new StreamWriter("config.txt");
                sw.WriteLine("CONNECT: " + source);
                sw.WriteLine("MINVIEW: " + minviews);
                sw.WriteLine("MAXVIEW: " + maxviews);
                string tempselect = selectnum.Replace("top", "").Replace("*", "").Trim();
                int intselect;
                if (int.TryParse(tempselect, out intselect))
                {
                    sw.WriteLine("SELECT#: " + source);
                }
                else
                {
                    sw.WriteLine("SELECT#: *");
                }
                sw.WriteLine("WAITMIL: " + waittime);
                sw.WriteLine("NEWVIEW: " + ensurenewview);

                sw.Close();
            }
            Console.WriteLine("Done.  " + totallinks + " links scanned, " + brokenlinks + " broken links found.  Close whenever");
        }
        public static void CheckFiles()
        {
            string curloc = Directory.GetCurrentDirectory();
            string[] rts = Directory.GetFiles(curloc + "\\RTs\\");
            //Console.WriteLine(rts.Length);

            //26
            for (int i = 0; i < rts.Length; i++)
            {
                Console.WriteLine("Checking " + rts[i]);
                string filename = rts[i].Substring(curloc.Length + 5);
                string ext = filename.Substring(filename.Length - 4);
                if (!string.Equals(ext, ".txt"))
                {
                    Console.WriteLine("Only .txt files allowed - " + rts[i]);
                    wrongformat.Add(filename + ext);
                    continue;
                }
                filename = filename.Substring(0, filename.Length - 4);

                string curfile = File.ReadAllText(rts[i]);
                while (curfile.IndexOf("streamable.com") != -1)
                {
                    
                    int pos = curfile.IndexOf("streamable.com");
                    curfile = curfile.Substring(pos);

                    //determine end of streamable link
                    int space = curfile.IndexOf(" ");
                    int par = curfile.IndexOf(")");
                    int quote = curfile.IndexOf("\"");

                    if (space == -1)
                    {
                        space = int.MaxValue;
                    }
                    if (par == -1)
                    {
                        par = int.MaxValue;
                    }
                    if (quote == -1)
                    {
                        quote = int.MaxValue;
                    }

                    if (space > par)
                    {
                        space = par;
                    }
                    if (space > quote)
                    {
                        space = quote;
                    }
                    
                    if (space == int.MaxValue)
                    {
                        curfile = "";
                        continue;
                    }

                    string link = "https://" + curfile.Substring(0, space);
                    curfile = curfile.Substring(space);
                    //Console.WriteLine(link);

                    //Get link from database
                    string query = "Select * FROM [Streamable].[dbo].[Links] " +
                        " where link like '%" + link + "%'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = query,
                        CommandTimeout = 10
                    };

                    var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                    DataTable dt = new DataTable();
                    sqlAdapt.Fill(dt);

                    bool found = false;
                    for (int s = 0; s < dt.Rows.Count; s++)
                    {
                        string sqllink = dt.Rows[s]["link"].ToString();
                        if (sqllink.Equals(link))
                        {
                            string source = dt.Rows[s]["source"].ToString();
                            if (string.IsNullOrEmpty(source))
                            {//Double check that the source is stored
                                string sqlid = dt.Rows[s]["id"].ToString();
                                string update = "Update [Streamable].[dbo].[Links] set source = '" + filename + "' where id = " + sqlid;

                                var sqlUpdate = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = update,
                                    CommandTimeout = 10
                                };
                                sqlUpdate.ExecuteNonQuery();
                            }
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        continue;
                    }
                    
                    //Wasn't found in sql, Insert it into the database
                    string insert = "insert into [Streamable].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                        "('" + link + "', '" + filename + "', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";

                    var sqlProcedureInsert = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = insert,
                        CommandTimeout = 10
                    };
                    sqlProcedureInsert.ExecuteNonQuery();
                }

                loaded.Add(filename + ext);
                File.Delete(rts[i]);
            }
        }
        public static void CheckLinks()
        {
            string query = "SELECT " + selectnum + " FROM[Streamable].[dbo].[Links] where views <= " + maxviews +
                " and views >= " + minviews + " order by lastviewd, views desc";
            
            var sqlProcedure = new SqlCommand
            {
                Connection = sql,
                CommandType = CommandType.Text,
                CommandText = query,
                CommandTimeout = 10
            };

            var sqlAdapt = new SqlDataAdapter(sqlProcedure);
            DataTable dt = new DataTable();
            sqlAdapt.Fill(dt);

            IWebDriver driver = new ChromeDriver();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow curRow = dt.Rows[i];
                string curURL = curRow["link"].ToString();
                string curID = curRow["id"].ToString();

                driver.Url = curURL;
                totallinks++;
                try
                {
                    driver.FindElement(By.Id("play-button")).Click();
                }
                catch (Exception ex)
                {
                    try
                    {
                        IWebElement videoplayer = driver.FindElement(By.Id("video-player-tag"));
                    }
                    catch (Exception ex3)
                    {
                        try
                        {
                            IWebElement notfound = driver.FindElement(By.XPath("//*[contains(., 'Oops')]"));
                            Console.WriteLine("Broken video detected - " + curURL);
                            burl.Add(curURL);
                            bsource.Add(curRow["source"].ToString());
                            brokenlinks++;
                            string insert = "insert into [Streamable].[dbo].[Broken] ([link], [source], [timestamp]) values " +
                        "('" + curURL + "', '" + curRow["source"].ToString() + "', '" + DateTime.Now.ToShortDateString() + "')";

                            var sqlProcedureInsert = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = insert,
                                CommandTimeout = 10
                            };
                            sqlProcedureInsert.ExecuteNonQuery();

                            string delete = "Delete from [Streamable].[dbo].[Links] where id =" + curID;
                            var sqlProcedureDelete = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = delete,
                                CommandTimeout = 10
                            };
                            sqlProcedureDelete.ExecuteNonQuery();
                        }
                        catch (Exception ex2)
                        {
                            //Not broken, just being weird.  continue on without trying anything
                        }
                        continue;
                    }
                }
                bool newview = false;

                IWebElement views = driver.FindElement(By.Id("visits"));
                string strviews = views.Text.Replace("views", "").Trim();
                int numviews = int.Parse(strviews);

                Stopwatch sw = new Stopwatch();
                while (!newview && sw.Elapsed.TotalSeconds < 90 && ensurenewview && numviews <= 100)
                {
                    ChromeOptions opt = new ChromeOptions();
                    opt.AddArgument("--autoplay-policy=no-user-gesture-required");
                    IWebDriver sdriver = new ChromeDriver(opt);
                    sdriver.Url = curURL;

                    IWebElement views2 = sdriver.FindElement(By.Id("visits"));
                    string strviews2 = views2.Text.Replace("views", "").Trim();
                    int numviews2 = int.Parse(strviews2);

                    if (numviews2 > numviews)
                    {
                        newview = true;
                        numviews = numviews2;
                    }
                    else
                    {
                        Thread.Sleep(waittime);
                    }
                    sdriver.Close();
                }                

                string updatequery = "Update [Streamable].[dbo].[Links] set views = " + numviews + ", lastviewd = '" +
                           DateTime.Now.ToShortDateString() + "' where id = " + curID;

                //Console.WriteLine(updatequery);

                sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = updatequery,
                    CommandTimeout = 10
                };
                sqlProcedure.ExecuteNonQuery();

                Thread.Sleep(waittime);
            }
            driver.Close();
            return;            
        }
    }
}
