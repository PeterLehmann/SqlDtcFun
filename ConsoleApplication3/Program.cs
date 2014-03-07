using System;
using System.Data.SqlClient;
using System.Transactions;

namespace ConsoleApplication3
{
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using log4net;
    using log4net.Config;

    class Program
    {
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            Run();
            Run();
            Run();
            Console.ReadLine();
        }

        private static void Run()
        {
            try
            {
                using (var ts = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions
                                                                                         {
                                                                                             IsolationLevel =
                                                                                                 System.Transactions
                                                                                                 .IsolationLevel
                                                                                                 .Serializable,
                                                                                             Timeout =
                                                                                                 TimeSpan.FromMinutes(10)
                                                                                         }))
                {
                    Transaction.Current.EnlistDurable(DummyEnlistmentNotification.Id, 
                        new DummyEnlistmentNotification(),
                        EnlistmentOptions.EnlistDuringPrepareRequired);
                    using (
                        var con1 =
                            new SqlConnection(
                                "Data Source=.;Initial Catalog=trio;Integrated Security=True;max pool size = 2"))

                    using (
                        var con2 =
                            new SqlConnection(
                                "Data Source=.;Initial Catalog=master;Integrated Security=True;max pool size = 2")
                        )
                    {
                        con1.Open();
                        con2.Open();
                        using (var tx1 = con1.BeginTransaction(IsolationLevel.Serializable))
                        using (var tx2 = con2.BeginTransaction(IsolationLevel.Serializable))
                        {
                            var cmd = con1.CreateCommand();
                            cmd.CommandText = @"INSERT INTO [dbo].[CommonCodeDomain]
           ([CommonCodeDomainId]
           ,[DomainName]
           ,[Note]
           ,[CreatedDateTime]
           ,[CreatedBy]
           ,[ModifiedDateTime]
           ,[ModifiedBy]
           ,[Version])
     VALUES
           (@p1
           ,@p2
           ,@p3
           ,getdate()
           ,'user'
           ,getdate()
           ,'user'
           ,1)";
                            cmd.CommandType = CommandType.Text;
                            cmd.Prepare();


                            SetPar(cmd, "p1", new Guid("266424F8-0D04-46EE-B632-9C3552A6B649"));
                            SetPar(cmd, "p2", "test");
                            SetPar(cmd, "p3", "42");
                            var res = cmd.ExecuteNonQuery();
                            tx1.Commit();
                            tx2.Commit();
                        }
                    }
                    ts.Complete();
                }
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                Console.WriteLine(e);
            }
        }

        private static void SetPar(SqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
    }

    public class DummyEnlistmentNotification : IEnlistmentNotification
    {
        public static readonly Guid Id = new Guid("E2D35055-4187-4ff5-82A1-F1F161A008D0");
        private static readonly ILog logger = LogManager.GetLogger(typeof(DummyEnlistmentNotification));
        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            logger.DebugFormat("{0} {1} gettting call prepare", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId);
            Thread.Sleep(500);
            preparingEnlistment.Prepared();

        }

        public void Commit(Enlistment enlistment)
        {
            Thread.Sleep(500);
            enlistment.Done();
        }

        public void Rollback(Enlistment enlistment)
        {
            enlistment.Done();
        }

        public void InDoubt(Enlistment enlistment)
        {
            enlistment.Done();
        }
    }
}
