# Ping List Bot

This is an implementation of a ping list bot console application.
It allows to input ip addresses (targets) as a comma separated list and starts pinging them.
Additionally, for each reachable ip address, it checks for some http ports (80 and 8080 by default).

## Updating Targets and Ports

It is possible to add new targets and checked ports via the command line interface.
To remove a target or port, one has to type the corresponding target or port in the 'add' prompt.
If the target/port is already in the list it gets removed and is no longer pinged.
