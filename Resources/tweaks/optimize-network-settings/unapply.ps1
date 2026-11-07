 Write-Host "Reverting network tweaks to defaults..."
      netsh int tcp set heuristics enabled
      netsh int tcp set supp internet congestionprovider=default
      netsh int tcp set global rss=disabled
      netsh int tcp set global chimney=disabled
      netsh int tcp set global ecncapability=disabled
      netsh int tcp set global timestamps=enabled
      netsh int tcp set global initialRto=3000
      netsh int tcp set global rsc=enabled
      netsh int tcp set global nonsackttresiliency=enabled
      netsh int tcp set global MaxSynRetransmissions=5
      netsh int tcp set global fastopen=disabled
      netsh int tcp set global fastopenfallback=disabled
      netsh int tcp set global pacingprofile=normal
      netsh int tcp set global hystart=enabled
      netsh int tcp set global dca=disabled
      netsh int tcp set global netdma=disabled
      netsh int 6to4 set state state=disabled
      netsh int udp set global uro=disabled
      netsh winsock set autotuning default
      netsh int tcp set supplemental template=default icw=4
      netsh interface teredo set state disabled
      netsh int tcp set security mpp=enabled
      netsh int tcp set security profiles=enabled
      # Reset MTU to default (usually 1500 is default, so you might omit this or adjust)
      netsh interface ipv4 set subinterface "Wi-Fi" mtu=1500 store=persistent
      netsh interface ipv4 set subinterface Ethernet mtu=1500 store=persistent
      Write-Host "Network tweaks reverted."