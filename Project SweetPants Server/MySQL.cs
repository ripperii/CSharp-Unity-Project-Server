using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

namespace Project_SweetPants_Server
{
    class MySQL
    {
        private MySqlConnection connection;
        private string server;
        private string database;
        private string uid;
        private string password;

        //Constructor
        public MySQL()
        {
            server = "localhost";
            database = "project_sweetpants_database";
            uid = "root";
            password = "mircho";
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" + database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            connection = new MySqlConnection(connectionString);
            if (OpenConnection())
                writeline("Connection to MySQL DataBase has been established", ConsoleColor.Green);
        }
        static void writeline(object o, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.WriteLine(o);
            Console.ResetColor();
        }

        private bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                //When handling errors, you can your application's response based 
                //on the error number.
                //The two most common error numbers when connecting are as follows:
                //0: Cannot connect to server.
                //1045: Invalid user name and/or password.
                switch (ex.Number)
                {
                    case 0:
                        writeline("Cannot connect to MySQL server!",ConsoleColor.Red);
                        break;

                    case 1045:
                        writeline("Invalid MySQL Server username/password, please try again!", ConsoleColor.Red);
                        break;
                    default:
                        writeline("MySQL Error: " + ex, ConsoleColor.Red);
                        break;
                }
                return false;
            }
        }

        //Close connection
        private void CloseConnection()
        {
            try
            {
                connection.Close();
                
            }
            catch (MySqlException ex)
            {
                writeline("MySQL Error: " + ex.Message,ConsoleColor.Red);
                
            }
        }

        public List<List<string>> Select(string query)
        {

            //Create a list to store the result
            List<List<string>> list = new List<List<string>>();

            //string xmlString = "";
           
            var state = this.connection.State.ToString();
            if (state == "Open")
            {
                try
                {
                    //Create Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    //Create a data reader and Execute the command
                    MySqlDataReader dataReader = cmd.ExecuteReader();
                    List<string> fields = Enumerable.Range(0, dataReader.FieldCount).Select(dataReader.GetName).ToList();
                    //Read the data and store them in the list
                    while (dataReader.Read())
                    {
                        List<string> l = new List<string>();
                        for (int i = 0; i < dataReader.FieldCount; i++)
                        {
                            l.Add(dataReader[i].ToString());
                        }
                        list.Add(l);
                    }

                    //close Data Reader
                    dataReader.Close();
                    //xmlString = XMLClass.MakeXML(list,fields);
                    //return list to be displayed
                    return list;
                }
                catch(MySqlException)
                {
                    
                    if(this.connection.State == System.Data.ConnectionState.Closed)
                    {
                        this.CloseConnection();
                        this.OpenConnection();
                        return this.Select(query);            
                    }
                    else
                    {
                        return list;
                    }
                }
            }
            else
            {
                return list;
            }
        }

        public void Insert(string query)
        {

            //open connection
            var state = this.connection.State.ToString();
            if (state == "Open")
            {
                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //Execute command
                cmd.ExecuteNonQuery();
            }
            else
            {
                writeline("Connection has been closed for some reason?", ConsoleColor.Red);
            }
        }

        //Update statement
        public void Update(string query)
        {
            try
            {             //Open connection
                var state = this.connection.State.ToString();
                if (state == "Open")
                {
                    //create mysql command
                    MySqlCommand cmd = new MySqlCommand();
                    //Assign the query using CommandText
                    cmd.CommandText = query;
                    //Assign the connection using Connection
                    cmd.Connection = connection;

                    //Execute query
                    cmd.ExecuteNonQuery();


                }
                else
                {
                    writeline("Connection has been closed for some reason?", ConsoleColor.Red);
                }
            }
            catch(MySqlException)
            {
                Server.sql = new MySQL();
                Server.sql.Update(query);
            }

        }

        //Delete statement
        public void Delete(string query)
        {
            
            var state = this.connection.State.ToString();

            if (state == "Open")
            {
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.ExecuteNonQuery();
                
            }
            else
            {
                writeline("Connection has been closed for some reason?",ConsoleColor.Red);
            }
        }
    }
}
