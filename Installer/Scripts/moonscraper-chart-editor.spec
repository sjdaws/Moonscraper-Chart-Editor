Name:          #NAME#
Version:       #VERSION#
Release:       %autorelease
Summary:       #DESCRIPTION#
License:       BSD
Group:         Amusements/Games/Other
ExclusiveArch: x86_64

URL:           #URL#
BugURL:        #SUPPORT_URL#
Source0:       #URL#/archive/refs/tags/v%{version}.tar.gz

# Requires: bzip2-devel
 
%description
#DESCRIPTION#

%install
%make_install
 
%files
%doc AUTHORS NEWS.md README.md
%license LICENSE
%{_bindir}/%{name}
%{_mandir}/man1/%{name}.1*

https://github.com/sjdaws/Moonscraper-Chart-Editor/archive/refs/tags/v1.6.1.tar.gz