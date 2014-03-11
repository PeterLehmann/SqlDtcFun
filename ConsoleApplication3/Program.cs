namespace ConsoleApplication3
{
    using System;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Threading;
    using System.Transactions;
    using log4net;
    using log4net.Config;
    using IsolationLevel = System.Data.IsolationLevel;

    class Program
    {
        static void Main()
        {
            XmlConfigurator.Configure();

            Run();
            Run(); //SqlException: Distributed transaction completed. Either enlist this session in a new transaction or the NULL transaction.
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
                                                                                                 TimeSpan.FromMinutes(1)
                                                                                         }))
                {
                    Transaction.Current.EnlistDurable(DummyEnlistmentNotification.Id, 
                        new DummyEnlistmentNotification(),
                        EnlistmentOptions.EnlistDuringPrepareRequired); // only to make sure the transcation promotes to DTC
                    using (
                        var con1 =
                            new SqlConnection(
                                "Data Source=.;Initial Catalog=master;Integrated Security=True;max pool size = 2"))

                    using (
                        var con2 =
                            new SqlConnection(
                                "Data Source=.;Initial Catalog=master;Integrated Security=True;max pool size = 2;min pool size=1")
                        )
                    {
                        con1.Open();
                        con2.Open();
                        using (var tx1 = con1.BeginTransaction(IsolationLevel.Serializable))
                        using (var tx2 = con2.BeginTransaction(IsolationLevel.Serializable))
                        {
                            var cmd = con1.CreateCommand();
                            cmd.CommandTimeout = 30;
                            cmd.CommandText = @"waitfor delay '00:02:00'"; //get a timeout from sql server and now my SqlConnection is dead
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
    }

    public class DummyEnlistmentNotification : IEnlistmentNotification
    {
        public static readonly Guid Id = new Guid("E2D35055-4187-4ff5-82A1-F1F161A008D0");
        private static readonly ILog logger = LogManager.GetLogger(typeof(DummyEnlistmentNotification));
        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            logger.DebugFormat("{0} {1} gettting call prepare", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId);
            //Thread.Sleep(500);
            preparingEnlistment.Prepared();

        }

        public void Commit(Enlistment enlistment)
        {
            //Thread.Sleep(500);
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
