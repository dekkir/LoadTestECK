using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FM.WebSync;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
//using System.Windows.Forms;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;


namespace LoadTestECK
{
    /*
     * Создание кошелька и запись в файл информации о нем (адрес, публичный и приватный ключи)
     */
    [TestClass]
    public class ECK
    {
   
        static string baseURL = "http://dss3.idecide.io";
        static string baseURL_path = "dss3.idecide.io";

        static List<Wallet2> wallets = new List<Wallet2>();
        static List<string> transactionIdAccounts = new List<string>();
        static bool[] stateWallets = new bool[100]; //по умолчанию false

        static void writeFile(string s, int numberWallet)
        {
            try
            {
                string dateNow = DateTime.Now.Hour.ToString() + ":" + DateTime.Now.Minute.ToString() + ":" + DateTime.Now.Second.ToString();

                StreamWriter sw = new StreamWriter(@"D:\wallet\" + baseURL_path + ".txt", true);
                //sw.WriteLine(dateNow);
                //sw.WriteLine("Порядковый номер кошелька: " + numberWallet);
                sw.WriteLine(s + "\n");
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }

        [TestMethod]
        public void CreateWallet()
        {
            /* 
             * создание 100 кошельков
             */
            for(int i = 0; i < 100; i++)
            {
                var dataWallet = getDataWallet(baseURL); //генерация нового кошелька
                wallets.Add(dataWallet.data.wallet); //запись данных кошелька в массив
                var signature = getDataSignature(dataWallet, baseURL); //сигнатура для создания нового кошелька 
                var account = getTransactionIdAccount(dataWallet.data.wallet, signature.data.sign, baseURL); //создание кошелька
                transactionIdAccounts.Add(account.data.transactionId); //запись транзакции создания кошелька, чтобы потом проверить успех
            }

            /* 
             * проверка
             */

            int j = 1; //порядоквый номер кошелька
            int k = 0; //счетчик времени, 240 раз == 20 минут (240 * 5 с)
            while(j < transactionIdAccounts.Count+1 && k < 240) //пока есть хоть одна транзакция, которую нужно проверить
            {
                for (int i = 0; i < transactionIdAccounts.Count; i++) //пробегаем по всем транзакциям
                {
                    if (!stateWallets[i])
                    {
                        var state = getStateAccount(transactionIdAccounts[i], baseURL); //получение результата попытки создать
                        if (state.code.Equals("200") && !stateWallets[i])
                        {
                            //данные "подтвержденного" кошелька записываем в файл
                            writeFile("\naddress: " + wallets[i].address +
                                "\npublicKey: " + wallets[i].publicKey +
                                "\nprivateKey: " + wallets[i].privateKey + "\n______________", j);
                            j++;
                            stateWallets[i] = true; //транзакция успешна
                            break;
                        }
                    }
                    
                }
                Thread.Sleep(5000);
                k++;
            }
            
        }

        /*
         * Получение данных доя создания кошелька
         * Принимает адрес сервера АПИ
         */
        static HelperDataWallet getDataWallet(string baseUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string content = "";
            var body = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync($"{baseUrl}/api/v1/tests/create", body).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<HelperDataWallet>(responseContent);
        }

        /*
         * Получение сигнатуры для создания кошелька
         * Принимает информацию о кошельке и адрес сервера АПИ
         */
        static Signature getDataSignature(HelperDataWallet dw, string baseUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string content = JsonConvert.SerializeObject(new
            {
                wallet = dw.data.wallet,
                message = (dw.data.wallet.address).Substring(2) //обрезаем "0х"
            });
            var body = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync($"{baseUrl}/api/v1/tests/sign", body).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<Signature>(responseContent);
        }

        static Account getTransactionIdAccount(Wallet2 w, Sign s, string baseUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string content = JsonConvert.SerializeObject(new
            {
                address = w.address,
                sign = s
            });
            var body = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync($"{baseUrl}/api/v1/accounts", body).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<Account>(responseContent);
        }

        /*
         * Проверка выполнения транзакции на создание кошелька мастерчейном
         * Приннимает id транзакции и адрес сервера АПИ
         */
        static State getStateAccount(string transaction, string baseUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string content = JsonConvert.SerializeObject(new
            {
                transactionId = transaction
            });
            var body = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync($"{baseUrl}/api/v1/transactions/810/state", body).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<State>(responseContent);
        }

        

    }

    public sealed class HelperDataWallet
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForWallet data { get; set; }
    }

    public sealed class DataForWallet
    {
        public Wallet2 wallet { get; set; }
    }

    public sealed class Wallet2
    {
        public string address { get; set; }
        public string publicKey { get; set; }
        public string privateKey { get; set; }
    }

    public sealed class Signature
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForSignature data { get; set; }
 
    }

    public sealed class DataForSignature
    {
        public Sign sign { get; set; }  
    }

    public sealed class Sign
    {
        public string s { get; set; }
        public string r { get; set; }
        public int v { get; set; }      
    }

    public sealed class Account
    {
        public string code { get; set; }
        public string message { get; set; }
        public DataForAccounts data { get; set; }
    }
    public sealed class DataForAccounts
    {
        public string transactionId { get; set; }
    }

    public sealed class State
    {
        public string code { get; set; }
        public string message { get; set; }
        public DataForState data { get; set; }
    }

    public sealed class DataForState
    {
        public string transactionId { get; set; }
        public int errorCode { get; set; }
        public string errorMessage { get; set; }
    }

  
}
