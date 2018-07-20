using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Collections;

namespace LoadTestECK
{
    [TestClass]
    public class WorkECK
    {
        public static int timeOutMinute = 30; //ожидание ответа в минутах
        public static int countFirstAPI = 10; //граница кошельков первой площадки
        public static int countSecondAPI = 20; //граница кошельков второй площадки

        [TestMethod]
        /*
         * Информация о кошельке
         */
         public void infoWallet()
        {
            StreamWriter sw = new StreamWriter("D:\\InfoWallet.txt", true); ;
            for (int i = 0; i < HelperWallet.address.Length; i++)
            {
                var info = getInfoWallet(HelperWallet.address[i], getBaseURL(i));
                sw.WriteLine(info.data.accountAddress + "\t" + info.data.balance.total + "\n");                
            }
            sw.Close();
        }


        [TestMethod]
        /*
         * Пополнение кошелька
         */
        public void replenishBalance()
        {
            List<string> transactions = new List<string>();
            //пополняем 300 кошльков и запоминает номер транзакции 
            for (int j = 0; j < 300; j++)
            {
                var resOfToPup = toPup(HelperWallet.address[j], getBaseURL(j));
                transactions.Add(resOfToPup.data.transactionId);
            }
            //проверяем успех  транзакции и пишем в файл
            for (int j = 0; j < 300; j++) { 
                for (int i = 0; i < 240; i++)
                {
                    var state = getState(transactions[j], getBaseURL(j)); //получение результата попытки пополнить кошелек
                    if (state.code.Equals("200")) break;
                    Thread.Sleep(5000);
                 }
            }             
        }

        [TestMethod]
        /*
         * Операция перевода денежных средств 
         */
        public void moneyTransfer()
        {
            
            int balance = 200000; //баланс отправителя
            int numberSender = HelperNumberWallet.number; //номер отправителя
            int r = 0; //итератор отправлений (10 штук)
            int t = 50; //количество транзакций

            //отправитель
            Wallet sender = new Wallet(HelperWallet.address[numberSender],
                                       HelperWallet.publicKey[numberSender],
                                       HelperWallet.privateKey[numberSender]);


            string [] transactions = new string[t]; //транзакция
            bool[] stateWallets = new bool[t]; //статус транзакции ( по умолчанию false)
            string[] recipients = new string[t]; //получатели (адреса)

            for (; r < t; r++) {
                /*
                 * Выбираем случайного получателя
                 */
                int numberRecipient = -1;
                do
                {
                    numberRecipient = new Random().Next(0, HelperWallet.address.Length);
                    if (numberSender != numberRecipient) break; //отправитель и получатель разные

                } while (true);

                /*
                 * Выполняем подготовку денежных средств
                 */
                             
                int sum = 0;
                try
                {                  
                    sum = new Random().Next(1, (balance / 4)); //сумма для отправления от 1 до половины баланса
                    balance -= sum; //вычитаем пересланную сумму
                } catch(ArgumentOutOfRangeException ex)
                {
                    writeFileError(getDate(), sender.address, 
                                  HelperWallet.address[numberRecipient], "Ошибка формирования суммы отправления","-");                    
                    break;
                }
                
                /*
                 * Выполняем отправку денежных средств
                 */
                var signature = getSignature(getBaseURL(numberSender), sender, sum.ToString() + ".00", HelperWallet.address[numberRecipient]);
                if (signature == null) continue; //заканчиваем эту итерацию
                string startTimeAPI = getDate(); //отправка запроса к АПИ
                var resultTransfer = sentCash(getBaseURL(numberSender), 
                                              sender.address, new To("", HelperWallet.address[numberRecipient]), 
                                              (double)sum, signature.data);
                string endTimeAPI = getDate();//получение ответа от АПИ
                if (resultTransfer == null) continue; //заканчиваем эту итерацию
                
                transactions[r] = resultTransfer.data.transactionId; //транзакция
                recipients[r] = HelperWallet.address[numberRecipient];
                if (transactions[r].Equals(""))
                {
                    writeFileError(endTimeAPI, sender.address, HelperWallet.address[numberRecipient],
                        "АПИ не выдал id транзакции",
                        "message: " + resultTransfer.message + "code: " + resultTransfer.code.ToString());
                    continue;
                }
                writeFileAPI(numberSender, resultTransfer.data.transactionId, 
                            sender.address, HelperWallet.address[numberRecipient], 
                            sum, startTimeAPI, endTimeAPI); //запись результата
                
            }
            /*
             * Ожидаем результат выполнения транзакции
             */
            int i = 0;
            while (noTransactionStatusReceived(stateWallets) && i < 24686) //либо все транзакции получили статус 200, либо закончилось ожидание
            {
                for (int j = 0; j < r; j++) //по всем транзакциям (равны количеству отправлений)
                {
                    if (transactions[j].Equals("")) stateWallets[j] = true; //транзакция больше не проверяется
                    
                    if (!stateWallets[j]) //если ответ еще не получен на эту транзакцию
                    {
                        var state = getState(transactions[j], getBaseURL(numberSender)); //получить статус
                        if (state == null)
                        {
                            writeFileError(getDate(), sender.address, recipients[j],
                                           "Ошибка получения статуса транзакции " + transactions[j], "-");
                            stateWallets[j] = true; //транзакция больше не проверяется
                            continue; //завершаем эту итерацию
                        }
                        if (state.code.Equals("200")) //успех  транзакции
                        {
                            string endTimeMC = getDate(); //получение ответа от мастерчейн
                            stateWallets[j] = true; //транзакция успешна

                            if (state.data.errorCode != 0) //неуспех перевода денег (какая-либо ошибка)
                                writeFileError(endTimeMC, sender.address, recipients[j],
                                           "Не удалось выполнить перевод денег, errorCode " + state.data.errorCode, "-");
                            else  writeFileMC(numberSender, transactions[j], endTimeMC); //запись результата от МС
                        }
                    }                    
                }
                Thread.Sleep(7000);
                i++;
            }
        }

        /*
         * Пополнение кошелька
         * На вход принимает адрес кошелька и адрес сервера АПИ
         */
        static Pup toPup(string addr, string baseUrl)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string content = JsonConvert.SerializeObject(new
            {
                currencyCode = 810,
                address = addr,
                amount = 1000000
            });
            var body = new StringContent(content, Encoding.UTF8, "application/json");
            HttpResponseMessage response = client.PostAsync($"{baseUrl}/api/v1/tests/topup", body).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<Pup>(responseContent);
        }

        /*
         * Получение информации о кошельке
         * На вход принимает адрес кошелька и адрес сервера АПИ
         */
        static AllInfoWallet getInfoWallet(string addr, string baseUrl)
        {
            HttpResponseMessage response = null;
            try
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string content = JsonConvert.SerializeObject(new
                {
                    address = addr,
                });
                var body = new StringContent(content, Encoding.UTF8, "application/json");
                response = client.PostAsync($"{baseUrl}/api/v1/accounts/810/details", body).Result;             
                string responseContent = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<AllInfoWallet>(responseContent);
            } catch
            {
                writeFileError(getDate(), "-", "-", "Ошибка получения информации о кошельке",
                          "Статус код " + response.StatusCode.ToString() + "Успех запроса " + response.IsSuccessStatusCode);
                return null;
            }            
        }

        /*
         * Получение статуса транзакции
         * На вход принимает id транзакции и адрес сервера АПИ
         */
        static State getState(string transaction, string baseUrl)
        {
            HttpResponseMessage response = null;
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout.Add(new TimeSpan(0, timeOutMinute, 0));
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string content = JsonConvert.SerializeObject(new
                {
                    transactionId = transaction
                });
                var body = new StringContent(content, Encoding.UTF8, "application/json");
                response = client.PostAsync($"{baseUrl}/api/v1/transactions/810/state", body).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<State>(responseContent);
            }
            catch
            {
                return null;
            }           
        }

        /*
         * Получение сигнатуры для пересылки денег
         * На вход принимает адрес сервера АПИ
         *                   кошелек, с которого переводят денег
         *                   сумму перевода
         *                   адрес кошелька для перевода
         */
        static SignatureTransfer getSignature(string baseUrl, Wallet w, string sum, string addrTo)
        {
            HttpResponseMessage response = null;
            try
            {
                //w - кошелек, с которого будут переводиться деньги по адресу addrTo
                HttpClient client = new HttpClient();
                client.Timeout.Add(new TimeSpan(0, timeOutMinute, 0));
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string content = JsonConvert.SerializeObject(new
                {
                    wallet = w,
                    message = "" + sum + (w.address).Substring(2) + addrTo.Substring(2) + 810
                });
                var body = new StringContent(content, Encoding.UTF8, "application/json");
                response = client.PostAsync($"{baseUrl}/api/v1/tests/sign", body).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<SignatureTransfer>(responseContent);
            }
            catch
            {
                writeFileError(getDate(), w.address, addrTo, "Ошибка получения сигнатуры",
                    "Статус код " + response.StatusCode.ToString() + " Успех запроса " + response.IsSuccessStatusCode);
                return null;
            }           
        }
        
        /*
         * Перевод денег с кошелька на кошелек
         * На вход принимает адрес сервера АПИ
         *                   адрес кошелька, с которого будет перевод
         *                   кошелек, куда переводят деньги
         *                   сумма
         *                   сигнатуру 
         */
        static Transfer sentCash(string baseUrl, string outOf, To into, double sum, DataForSignatureTransfer dst)
        {
            HttpResponseMessage response = null;
            try
            {
                HttpClient client = new HttpClient();
                client.Timeout.Add(new TimeSpan(0, timeOutMinute, 0));
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                string content = JsonConvert.SerializeObject(new
                {
                    from = outOf,
                    to = into,
                    amount = sum,
                    sign = dst.sign
                });
                var body = new StringContent(content, Encoding.UTF8, "application/json");
                response = client.PostAsync($"{baseUrl}/api/v1/transactions/810/create", body).Result;
                string responseContent = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<Transfer>(responseContent);
            }
            catch
            {
                writeFileError(getDate(), outOf, into.address, "Ошибка перевода средств",
                    "Статус код " + response.StatusCode.ToString() + " Успех запроса " + response.IsSuccessStatusCode);
                return null;
            }
        }

        /*
         * Запись в файл
         * Принимает номер кошелька (не адрес)
         *           адрес кошелька
         *           баланс кошелька
         */
        static void writeFile(string numberWallet, string addr, string balance)
        {
            try
            {
                StreamWriter sw = new StreamWriter("D:\\Wallet " + numberWallet + ".txt", true);

                sw.WriteLine("____________________________________________________________________");
                sw.WriteLine("Адрес кошелька: " + addr);
                sw.WriteLine("Баланс кошелька: " + balance);
                sw.WriteLine("____________________________________________________________________");

                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }

        /*
         * Запись в файл ошибки
         * Принимает дату
         *           адрес кошелька, с которым производились действия
         *           адрес кошелька, куда пересылали деньги
         *           сообщение об ошибке
         *           сообщение об ошибке HTTP
         */
        static void writeFileError(string date, string from, string to, string message, string messageExtend)
        {
            try
            {
                StreamWriter sw = new StreamWriter(@"D:\ErrorDSS\" + from + ".txt", true);
                sw.WriteLine(date + ";" + from + ";" + to + ";" + message + ";" + messageExtend);
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }

        /*
         * Метод записывает в файл информацию о произведенных транзакциях
         * @param numberWallet - номер кошелька, на его снове создается файл для записи
         * @param numberTransaction - номер транзакции
         * @param timeStart - время отправки транзакции к АПИ 
         * @param timeEnd - время получения ответа 200 ОК от АПИ 
         */
        static void writeFileAPI(int numberWallet, string numberTransaction, string from, string to, int sum, string timeStart, string timeEnd)
        {
            try
            {
                StreamWriter sw =  new StreamWriter("D:\\ApiDSS\\API_Wallet " + numberWallet + ".txt", true);
                sw.WriteLine(numberTransaction + ";" + from + ";" + to + ";" + sum + ";" + timeStart + ";" + timeEnd);
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }

        /*
         * Запись в файл информации по мастерчейну
         * Принимает номер кошелька (не адрес)
         *           номер транзакции
         *           время ответа от мастерчейна по поводу выполнения транзакции
         */
        static void writeFileMC(int numberWallet, string numberTransaction, string timeEnd)
        {
            try
            {
                StreamWriter sw = new StreamWriter("D:\\McDSS\\MC_Wallet " + numberWallet + ".txt", true);
                sw.WriteLine(numberTransaction + ";" + timeEnd);
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }


        static string getDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.FFF");
        }

        /*
         * Возвращает базовую сслку АПИ
         * Если numberWallet (0, 99) - 1 ссылка
         * Если numberWallet (100, 199) - 2 ссылка
         * Если numberWallet (200, 299) - 3 ссылка
         */
        static string getBaseURL(int numberWallet)
        {
           // if(numberWallet % 3 == 0) return "http://dss.idecide.io";
           // if(numberWallet )
            if (numberWallet < countFirstAPI) return "http://dss.idecide.io";
            if(numberWallet < countSecondAPI) return "http://dss2.idecide.io";
            return "http://dss3.idecide.io";
        }

        /*
         * Проверяет, есть ли еще транзакции, статус которых еще не равен 200 (не обработан мастерчейном)
         */
        static bool noTransactionStatusReceived(bool[] b)
        {
            return Array.Exists(b, element => element == false);
        }
    }

    public sealed class Pup
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForPup data { get; set; }
    }

    public sealed class DataForPup
    {
        public string transactionId { get; set; }
    }

    public sealed class AllInfoWallet
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForAllInfoWallet data { get; set; }

    }
      
    public sealed class DataForAllInfoWallet
    {
        public string accountAddress { get; set; }
        public int state { get; set; }
        public int juridicalType { get; set; }
        public int identityType { get; set; }
        public Balance balance { get; set; }
        public Limits limits { get; set; }
    }

    public sealed class Balance
    {
        public Items [] items { get; set; }
        public int total { get; set; }
    }

    public sealed class Limits
    {
        public int operation { get; set; }
        public int daily { get; set; }
        public int monthly { get; set; }
        public int balance { get; set; }

    }

    public sealed class Items
    {
        public string address { get; set; }
        public string name { get; set; }
        public string bik { get; set; }
        public int amount { get; set; }

    }

    public sealed class SignatureTransfer
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForSignatureTransfer data { get; set; }
    }

    public sealed class DataForSignatureTransfer
    {
        public SignForTransfer sign { get; set; }
    }

    public sealed class SignForTransfer
    {
        public string s { get; set; }
        public string r { get; set; }
        public int v { get; set; }
    }

    public sealed class Transfer
    {
        public int code { get; set; }
        public string message { get; set; }
        public DataForTransfer data { get; set; }
    }

    public sealed class DataForTransfer
    {
        public string transactionId { get; set; }
    }

     /*
      * 7. Операция перевода денежных средств, параметр To
     */
    public  class To
    {
        public string identifier;
        public string address;

        public To(string i, string a)
        {
            identifier = i;
            address = a;
        }
    }
}
