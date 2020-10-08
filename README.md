# SQLStreamable

Kyr's new and improved Streamable viewer bot.

**********************

How to use:

1.)  Download SQL Server

https://www.microsoft.com/en-us/sql-server/sql-server-downloads

2.)  Update the connection string in the config file.  Replace "KyrarycPC" with the name of your computer.  If your computer is password protected, add the password there.  Don't worry, it won't send any info to me.

More about connection strings here:

https://www.connectionstrings.com/sql-server/

3.)  Either add a link to the respect thread to the RTLinks.txt file or add the a txt file with the Markdown code inside the RTs folder.  Links work better with old.reddit

4.)  Run SqlStreamable.exe

5.)  It should automatically assemble the database and tables, then populate them based on the respect threads given.

*************************************

Config file

*************************************

* CONNECT: The SQL connection string to be used.  My initial connection string is "Data Source=KyrarycPC;Integrated Security=SSPI;"

After setup is complete, my connection string is "Data Source=KyrarycPC;Integrated Security=SSPI;Initial Catalog=Streamable;"

* MINVIEW: The minimum number of views required for a video to be checked.  Useful to provided extra options.  Recommended to be set to 0 to check every video.

* MAXVIEW: The maximum number of views a video is allowed to have and still be checked.  Any video saved in the database with a higher view count will not be checked.  Recommended to be at least 100.

* SELECT#: Controls how many videos are to be checked.  Use * to check every video that meets the view requirements.  Otherwise, any number will work.

* WAITMIL: Used to determine how long to let a video play before moving on or reloading.  Default is 1500 ms (1.5 s)

* NEWVIEW: If set to true, the script will continously open new chrome browsers until the view count on each video changes or 90 seconds have passed.  Using True should guarantee that each video recieves a new view, but it will drastically increase the total time it takes to completely check every video.
