using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sqlite.Model
{
    public class Contacts
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string CreationDate { get; set; }
        public Contacts()
        { }
        public Contacts(string name,string phone_no)
        {
            Name = name;
            PhoneNumber = phone_no;
            CreationDate = DateTime.Now.ToString();
        }
    }
}
