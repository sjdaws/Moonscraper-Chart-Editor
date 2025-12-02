Name:          #PACKAGE#
Version:       #VERSION#
Release:       1
License:       BSD
Vendor:        #PUBLISHER#
Summary:       #DESCRIPTION#
Group:         Amusements/Games/Other
ExclusiveArch: x86_64
Requires:      findutils

URL:           #URL#
BugURL:        #SUPPORT_URL#
VCS:           #URL#/tree/v${version}

%description
#DESCRIPTION#

%define _missing_build_ids_terminate_build 0
%define _rpmdir .
%define _target_os linux

%pre
if [ ! -d "/usr/local/share/applications" ]; then
  mkdir -p /usr/local/share/applications
  chmod 755 /usr/local/share/applications
fi

%files
%defattr(644, -, -, 755)
%dir /opt/#PACKAGE_PATH#
%dir /opt/#PACKAGE_PATH#/Config
%dir /opt/#PACKAGE_PATH#/Custom?Resources
%dir /opt/#PACKAGE_PATH#/Documentation
%dir /opt/#PACKAGE_PATH#/#NAME_PATH#_Data
/opt/#PACKAGE_PATH#/*.*
/opt/#PACKAGE_PATH#/Config/*
/opt/#PACKAGE_PATH#/Custom?Resources/*
%doc /opt/#PACKAGE_PATH#/Documentation/*
/opt/#PACKAGE_PATH#/#NAME_PATH#_Data/*
%license /opt/#PACKAGE_PATH#/LICENSE
%defattr(755, -, -, 755)
/opt/#PACKAGE_PATH#/#NAME_PATH#
%defattr(644, -, -, 755)
/usr/local/share/applications/#NAME_PATH#.desktop
