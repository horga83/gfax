//  GFAX - Gnome fax application
//  Copyright (C) 2003 - 2008 George A. Farris
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Library General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

namespace gfax {
	using Mono.Unix;
	using System;
	using System.IO;
	using System.Text;
	using System.Collections;
	
	// Phone book that holds multiple numbers
	public class Phonebook
	{
		public string Name;	//Name book is called by
		public string Path;	//Filename including path
		public string Type; // Type of phone book (gfax,gcard etc)
	}
	
	// Individual contact in phone book
	public class GfaxContact
	{
		public string Organization;
		public string PhoneNumber;
		public string ContactPerson;
	}
	
	public class Phonetools
	{
		public static Phonebook[] get_phonebooks ()
		{
			StreamReader infile = null;
			string buf;
			bool migrate = false;
			
			Phonebook[] pbooks = null;
						
			// TODO  get location from gconf
			string HOMEDIR = Environment.GetEnvironmentVariable("HOME");
			string PHONEBOOKS = gfax.ConfigDirectory + "/phonebooks";
			
			#if DEBUG
				Console.WriteLine("[Phonebook] Reading phonebook file");
			#endif
			
			int numberOfBooks = get_number_of_books();
			pbooks = new Phonebook[numberOfBooks];			
			
			if (numberOfBooks == 0) {
				return (pbooks);
			}
			
				
			try { 
				infile = File.OpenText(PHONEBOOKS); 
				int i = 0;
				
				while ( (buf = infile.ReadLine()) != null ) {
					switch (buf.Trim()) {
						case "<book>" :
							// TODO more robust file reading
							Phonebook c = new Phonebook();
							c.Name = strip_tag(infile.ReadLine(), "name");
							c.Type = strip_tag(infile.ReadLine(), "type");
							c.Path = strip_tag(infile.ReadLine(), "path");
							
							// Migrate from old location
							if (c.Type == "gfax") {
								if (Path.GetDirectoryName(c.Path) == HOMEDIR + "/.etc/gfax") {
									c.Path = gfax.ConfigDirectory + "/" + Path.GetFileName(c.Path);
									migrate = true;
								}
							}
							
							pbooks[i++] = c;
							continue;
					}
				}

				infile.Close();
				if (migrate) 
					save_phonebooks(pbooks);
				return pbooks;			
			}
			catch (Exception e) { 
				Console.WriteLine("get_phonebooks - Exception in phonebook.cs {0}", e);
				return pbooks; 
			}

		}
		
		// save or create phonebooks files
		public static void save_phonebooks (Phonebook[] pbooks)
		{
			StreamWriter outfile;
									
			// TODO  get location from gconf
			string PHONEBOOKS = gfax.ConfigDirectory + "/phonebooks";
			
			try { 
				outfile = File.CreateText(PHONEBOOKS); 
			} catch (Exception e) { 
				Console.WriteLine("Exception in phonebook.cs {0}", e);
				return; 
			}

			outfile.WriteLine("<gfax>");
			Console.WriteLine("Len :{0}", pbooks.Length);
			foreach (Phonebook p in pbooks) {
				outfile.WriteLine("	<book>");
				outfile.WriteLine("		<name>" + p.Name + "</name>");
				outfile.WriteLine("		<type>" + p.Type + "</type>");
				outfile.WriteLine("		<path>" + p.Path + "</path>");
				outfile.WriteLine("	</book>");
			}
			outfile.WriteLine("</gfax>");
			outfile.Close();
		}
		
		public static void delete_book (string book)
		{
			StreamReader infile = null;
			string buf;
			string deleteme = null;
			Phonebook[] pbooks = null;
			
			// make array size less 1 because we're deleting 1
			pbooks = new Phonebook[get_number_of_books() - 1];
			
			// TODO  get location from gconf
			string PHONEBOOKS = gfax.ConfigDirectory + "/phonebooks";
			string type = "";
			try { 
				infile = File.OpenText(PHONEBOOKS); 
				int i = 0;
			
				// iterate through the phonebook file and skip past book to delete
				while ( (buf = infile.ReadLine()) != null ) {
					switch (buf.Trim()) {
						case "<book>" :
							// TODO more robust file reading
							Phonebook c = new Phonebook();
							c.Name = strip_tag(infile.ReadLine(), "name");
							c.Type = strip_tag(infile.ReadLine(), "type");
							c.Path = strip_tag(infile.ReadLine(), "path");
						
							if (c.Name != book)
								pbooks[i++] = c;
							else {
								deleteme = c.Path;
								type = "gfax";
							}
							continue;
					}
				}
				infile.Close();
				save_phonebooks(pbooks);
				
				if (type == "gfax")
					if (File.Exists(deleteme))
						File.Delete(deleteme);
			}
			catch (Exception e) {
				Console.WriteLine("delete_book - Exception in phonebook.cs {0}", e);
				return; 
			}
		}
		
		// Create or add the new phone book to the "phonebooks" file.
		public static void add_book (Phonebook p)
		{
			StreamReader infile = null;
			string buf;
			Phonebook[] pbooks = null;
			
			// make array plus 1 because we're adding 1
			pbooks = new Phonebook[get_number_of_books() + 1];
			
			// TODO  get location from gconf
			string PHONEBOOKS = gfax.ConfigDirectory + "/phonebooks";
			
			if (p.Type == "gfax") {
				// add default path if not specified
				if (Path.GetDirectoryName(p.Path) == "")
					p.Path = gfax.ConfigDirectory + p.Path;
			}
				// Create the file.
			if (!File.Exists(PHONEBOOKS)) {
				FileStream fs = File.Create(PHONEBOOKS); 
				fs.Close();
			}

			int i = 0;
			try { 
				infile = File.OpenText(PHONEBOOKS); 
				
				while ( (buf = infile.ReadLine()) != null ) {
					switch (buf.Trim()) {
						case "<book>" :
							// TODO more robust file reading
							Phonebook c = new Phonebook();
							c.Name = strip_tag(infile.ReadLine(), "name");
							c.Type = strip_tag(infile.ReadLine(), "type");
							c.Path = strip_tag(infile.ReadLine(), "path");
										
							pbooks[i++] = c;
							continue;
					}
				}
				infile.Close();
			}
			catch (Exception e) { 
				// TODO catch file ops error
				Console.WriteLine("add_book - Exception in phonebook.cs {0}", e);
				return;
			}

			
			pbooks[i++] = p;	// add the new book
			save_phonebooks(pbooks);
		}

		// save a list (ArrayList) of contacts
		public static void save_phonebook_items (string book, ArrayList contacts)
		{
			Phonebook p;
			StreamWriter outfile;
						
			p = get_book_from_name(book);
			
			if (p.Type == "gfax") {
			// TODO error reporting
				try { outfile = File.CreateText(p.Path); }
				catch (Exception e) {
					Console.WriteLine("save_phonebook_items - Exception in phonebook.cs {0}", e);
					return; 
				}
			
				outfile.WriteLine("#Gfax phone book");
			
				IEnumerator enu = contacts.GetEnumerator();
				while ( enu.MoveNext() ) {
					GfaxContact c = new GfaxContact();
					c = (GfaxContact)enu.Current;
					outfile.WriteLine("{0}:{1}:{2}",c.PhoneNumber,c.ContactPerson,c.Organization);
				}
				outfile.Close();
			}
		}
		
		public static StreamReader open_phonebook (Phonebook p)
		{
			StreamReader infile = null;
			
			// If it doesn't exist yet just return
			if (!File.Exists(p.Path))
				return(null);
				
			try { 
				infile = File.OpenText(p.Path);
			}
			catch (Exception e) { 
				Console.WriteLine("open_phonebook - Exception in phonebook.cs {0}", e);
				return null;
			}
			return infile;
		}
		
		public static ArrayList get_contacts (Phonebook p)
		{
			string buf = null;
			string[] sa;
			//char[] ca = {':',':',':'}; delete me
			
			ArrayList records = new ArrayList();			
			StreamReader fp = null;

			// TODO add popup message
			if ( p.Type == "gfax" ) {
				fp = open_phonebook(p);
				if (fp == null) {
					Console.WriteLine(Catalog.GetString("Can't open file : {0}"), p.Path);
					return records;
				}

				while ( (buf = fp.ReadLine()) != null ) {
					buf.Trim();
				
					if (buf[0] == '#')
						continue;
					else {
						sa = buf.Split(':');
						GfaxContact contact = new GfaxContact();
						contact.PhoneNumber = sa[0];
						contact.ContactPerson = sa[1];
						contact.Organization = sa[2];
						records.Add(contact);
					}
				}
					
				fp.Close();
			}
			
			if ( p.Type == "evolution" ) {
				EdsPhoneBooks eds = new EdsPhoneBooks();
				ArrayList ebooks = new ArrayList();
				ebooks = eds.GetPhoneBooks();
				
				IEnumerator enu = ebooks.GetEnumerator();
				while ( enu.MoveNext() ) {
					if ((string)enu.Current == p.Name) {
						records = eds.GetContacts((string)enu.Current);
					}
				}
			}
			
			return records;
			
		}
		
		public static string strip_tag (string line, string tag)
		{
			string bt = "<" + tag + ">";
			string et = "</" + tag + ">";
			string s = (line.Trim()).Replace(bt, "");
			return s.Replace(et, "");
		}
		
		private static int get_number_of_books ()
		{
			StreamReader infile = null;
			string buf;
			int count = 0;
			
			// TODO  get location from gconf
			string PHONEBOOKS = gfax.ConfigDirectory + "/phonebooks";
			
			if (!File.Exists(PHONEBOOKS))
				return(0);
				
			try { 
				infile = File.OpenText(PHONEBOOKS); 
			} catch (Exception e) { 
				Console.WriteLine("get_number_of_books - Exception in phonebook.cs {0}", e);
				return 0; 
			}
			
			// how many phone books do we have, Trim() removes white space
			while ( (buf = infile.ReadLine()) != null ) {
				if ( buf.Trim() == "<book>") {
					count++;
				}
			}
			infile.Close();
			return count;
		}
		
		public static Phonebook get_book_from_name(string name)
		{
			Phonebook[] books;
			//int count; delete me
			
			//count = get_number_of_books(); delete me
			books = get_phonebooks();
			
			foreach (Phonebook p in books) {
				if (name == p.Name)
					return p;
			}
			return null;
		}
	}
}
