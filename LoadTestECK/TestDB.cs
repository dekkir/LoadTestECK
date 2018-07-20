using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace LoadTestECK
{
    [TestClass]
    public class TestDB
    {
        [TestMethod]
        public void testDB()
        {
            string connString = "Host=52.234.148.53;Username=dss;Password=dss!23456;Database=dss";
            using (var connection = new NpgsqlConnection(connString))
            {

                connection.Open();
                 NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM example", connection);
                NpgsqlDataReader dataReader = command.ExecuteReader();
                string str = dataReader[0].ToString();
/*
                NpgsqlCommand command = new NpgsqlCommand("CREATE TABLE example (name char(100) PRIMARY KEY );", connection);
                command.ExecuteNonQuery();
                // Insert some data
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = "INSERT INTO example VALUES (@p)";
                    cmd.Parameters.AddWithValue("p", "Hello world");
                    cmd.ExecuteNonQuery();
                }
*/
            }

           
        }

        public static void writeFile(string str)
        {
            try
            {
                StreamWriter sw = new StreamWriter(@"C:\Users\p.ignatenko-x.RTS-TENDER\testdb.txt", true);

                sw.WriteLine(str);

                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }
    }
}
