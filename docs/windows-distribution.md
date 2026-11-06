# Εγκατάσταση και υπογραφή στα Windows

Η έκδοση διανομής περιλαμβάνει δύο αρχεία: υπογεγραμμένο εγκαταστάτη Windows και πλήρες υπογεγραμμένο ZIP για τις αναβαθμίσεις μέσα από την εφαρμογή. Ο εγκαταστάτης τοποθετεί μόνο αρχεία εφαρμογής στο `%LOCALAPPDATA%\Programs\AllInsurance Backoffice`. Η βάση, τα αντίγραφα ασφαλείας και τα αρχεία χρήστη παραμένουν κάτω από `%LOCALAPPDATA%\AllInsurance` και δεν περιλαμβάνονται στον εγκαταστάτη.

Για δημόσια διανομή χρειάζεται πιστοποιητικό υπογραφής κώδικα από πάροχο που εμπιστεύονται τα Windows. Δημιουργήστε στο GitHub repository τα Actions secrets `CODE_SIGNING_PFX_BASE64` και `CODE_SIGNING_PFX_PASSWORD`. Το πρώτο είναι η κωδικοποίηση Base64 του αρχείου PFX· το δεύτερο είναι ο κωδικός του. Μην τοποθετείτε το PFX ή τον κωδικό στο repository. Για εμπορική χρήση του Inno Setup απαιτείται άδεια από τον δημιουργό του και το αντίστοιχο secret `INNO_LICENSE_KEY`. Το workflow `Desktop release` σταματά αν λείπουν τα secrets ή αν η υπογραφή δεν θεωρείται έγκυρη.

Η δημιουργία tag `v0.7.13` εκκινεί το workflow. Το workflow υπογράφει τα εκτελέσιμα και τις βιβλιοθήκες της εφαρμογής, δημιουργεί τον εγκαταστάτη με υπογεγραμμένο πρόγραμμα απεγκατάστασης, και ανεβάζει τον εγκαταστάτη και το ZIP στο GitHub Release.

Η υπογραφή αποδεικνύει τον εκδότη και βοηθά τους ελέγχους των Windows. Δεν εγγυάται ότι κάθε σύστημα ή προϊόν ασφαλείας θα αποδεχθεί αμέσως ένα νέο αρχείο. Αν το Microsoft Defender επισημάνει συγκεκριμένο υπογεγραμμένο αρχείο ως κακόβουλο, υποβάλετέ το για ανάλυση ως πιθανώς λανθασμένη ανίχνευση στο [Microsoft Security Intelligence](https://www.microsoft.com/wdsi/filesubmission). Μην ζητάτε από χρήστες να απενεργοποιήσουν την προστασία τους.

Πηγές: [υπογραφή και Smart App Control](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control), [επιλογές διανομής Windows](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path), [εμπορική άδεια Inno Setup](https://jrsoftware.org/isorder.php).
