<Query Kind="Expression">
  <Connection>
    <ID>a0e7b2bd-baa1-4ecd-bba0-f691853639c3</ID>
    <NamingServiceVersion>2</NamingServiceVersion>
    <Persist>true</Persist>
    <Server>SNOWBALL\MSSQLSERVER01</Server>
    <Database>Photos</Database>
  </Connection>
</Query>

from c in CheckSums
						where c.SHA != null
						group c by c.SHA
						into g
						where g.Count() > 1
						//orderby g.Count() descending
						select new { hashVal = g.Key, Count = g.Count()}
								  
								  