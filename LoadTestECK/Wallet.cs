using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/**
 * 7. Получение сигнатуры для операции перевода денежных средств
 * Параметр Wallet
 */
namespace LoadTestECK
{
    class Wallet
    {
        public string address;
        public string publicKey;
        public string privateKey;

            
       public Wallet(string a, string pubK, string prK)
        {
            address = a;
            publicKey = pubK;
            privateKey = prK;
        }
    }
}
