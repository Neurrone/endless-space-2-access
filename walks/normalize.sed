s/\[droplist:-\?[0-9][0-9]*\//[droplist:#\//g
s/row-\?[0-9][0-9][0-9][0-9][0-9]*/row#/g
s/Clock, [0-9][0-9]*:[0-9][0-9] [AP]M/Clock, #TIME#/g
s/buf: [0-9][0-9]*:[0-9][0-9] [AP]M/buf: #TIME#/g
s/, [0-9][0-9]*:[0-9][0-9] [AP]M,/, #TIME#,/g
s/\\"defaultRead\\":\[[^]]*\]/\\"defaultRead\\":[#]/g
s/\/ship\/-\{0,1\}[0-9][0-9]*/\/ship\/#/g
s/design\/-\{0,1\}[0-9][0-9]*/design\/#/g
