# algetiming-streamer
Software for streaming timer events, and resulst though websockets into web-apps  
The protocol has bib-number in them, so they will also be a part of the streaming process.  
  
## The Timmy protocol from USB
examples from the line  
  
*start* timer event with current time bib12(right) and bib78(left)  
`0012rC0 10:07:20.2731 00`  
`0078lC3 10:07:20.2731 00`  
  
*endtimer event* with used time for bib12 (right), and bib78(left)  
`0012rc1 00:00:09.2540 00`  
`0078lc4 00:00:05.9847 00`  
   
*end timer event* with current time for bib12 (right), and bib78(left)  
`0012rC1 10:04:35.6709 00`  
`0078lC4 10:04:32.4016 00`  

*feet up / no weight event* for bib12 (right), and bib78(left)  
`0012rC2 10:07:20.2731 00`  
`0078lC5 10:07:20.2731 00`  

## Known issues  
The false start is currently not recognized properly. It should actually be feet up / no weight events, which we should not stream to others.  
