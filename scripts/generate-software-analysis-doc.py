"""Generate the short Greek client overview from the current product description."""

from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "Ανάλυση λογισμικού - AllInsurance Backoffice.docx"
BLUE = RGBColor(17, 77, 130)
GRAY = RGBColor(77, 89, 102)


def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)


def add_table(doc, headings, rows, widths):
    table = doc.add_table(rows=1, cols=len(headings))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    for idx, (heading, width) in enumerate(zip(headings, widths)):
        cell = table.rows[0].cells[idx]
        cell.width = Cm(width)
        cell.text = heading
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        shade(cell, "DDEAF5")
        for run in cell.paragraphs[0].runs:
            run.bold = True
            run.font.color.rgb = BLUE
    for number, row in enumerate(rows):
        cells = table.add_row().cells
        for idx, (value, width) in enumerate(zip(row, widths)):
            cells[idx].width = Cm(width)
            cells[idx].text = value
            cells[idx].vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if number % 2:
                shade(cells[idx], "F3F7FA")
    doc.add_paragraph()


doc = Document()
section = doc.sections[0]
section.page_height, section.page_width = Cm(29.7), Cm(21)
section.top_margin = section.bottom_margin = Cm(1.75)
section.left_margin = section.right_margin = Cm(1.8)

styles = doc.styles
styles["Normal"].font.name = "Aptos"
styles["Normal"].font.size = Pt(9)
styles["Normal"].paragraph_format.space_after = Pt(4)
for name, size in [("Title", 19), ("Heading 1", 12), ("Heading 2", 10)]:
    styles[name].font.name = "Aptos"
    styles[name].font.size = Pt(size)
    styles[name].font.color.rgb = BLUE
    styles[name].paragraph_format.space_before = Pt(8)
    styles[name].paragraph_format.space_after = Pt(4)

title = doc.add_paragraph(style="Title")
title.add_run("Ανάλυση λογισμικού\nAllInsurance Backoffice Software")
subtitle = doc.add_paragraph("Συνοπτική παρουσίαση προς πελάτη  •  Έκδοση 0.5.0  •  03/11/2026")
subtitle.style.font.color.rgb = GRAY

doc.add_heading("1. Τι είναι η εφαρμογή", level=1)
doc.add_paragraph(
    "Το AllInsurance Backoffice Software είναι ελληνόφωνη εφαρμογή γραφείου για Windows, "
    "σχεδιασμένη για την καθημερινή οργάνωση ασφαλιστικών εργασιών. Συγκεντρώνει πελάτες, "
    "συνεργάτες, οχήματα, ασφαλιστήρια, ζημιές και οικονομικές κινήσεις σε συνδεδεμένες καρτέλες. "
    "Ο χρήστης συνδέεται με προσωπικό λογαριασμό και βλέπει τις ενότητες από το αριστερό μενού."
)

doc.add_heading("2. Τεχνολογία και αποθήκευση", level=1)
add_table(doc, ["Στοιχείο", "Περιγραφή"], [
    ("Περιβάλλον", "Εφαρμογή Windows με C#/.NET 10, WinUI 3 και Windows App SDK."),
    ("Δεδομένα", "Τοπική βάση SQLite με Entity Framework Core. Οι εγγραφές παραμένουν στον υπολογιστή του χρήστη."),
    ("Ασφάλεια", "Σύνδεση χρήστη, δικαιώματα διαχειριστή, αρχείο ελέγχου ενεργειών και δυνατότητα αντιγράφων ασφαλείας."),
], [3.1, 14.1])

doc.add_heading("3. Κύριες ενότητες", level=1)
add_table(doc, ["Ενότητα", "Χρήση"], [
    ("Πελάτες / Συνεργάτες", "Στοιχεία προσώπων, σχέσεις συνεργασίας, επικοινωνία και οικονομική εικόνα."),
    ("Οχήματα", "Τεχνικά στοιχεία, πρόσωπα οχήματος, σχετικά ασφαλιστήρια και ζημιές."),
    ("Εταιρείες / Πρακτορεία", "Στοιχεία ασφαλιστικών φορέων και διασύνδεση με τις ασφαλιστικές εγγραφές."),
    ("Ασφαλιστήρια", "Αιτήσεις, ασφαλιστήρια, ημερομηνίες κάλυψης, ανανεώσεις, καλύψεις και οικονομικά στοιχεία."),
    ("Ζημιές", "Καταχώριση και παρακολούθηση ζημιών που συνδέονται με πελάτες και οχήματα."),
    ("Λοιπές ενότητες", "Ενημέρωση, αναφορές και γέφυρες εταιρειών, με κοινή δομή λίστας και καρτέλας."),
], [4.2, 13.0])

doc.add_heading("4. Καρτέλα πελάτη: πεδία και σκοπός", level=1)
add_table(doc, ["Ομάδα", "Πεδία / τι εξυπηρετούν"], [
    ("Ταυτότητα", "ΑΦΜ, επώνυμο και όνομα (υποχρεωτικά), τύπος πελάτη, ταυτότητα: αναγνώριση φυσικού προσώπου ή επιχείρησης."),
    ("Προσωπικά", "Γέννηση και αυτόματη ηλικία, φύλο, γονείς, ιθαγένεια, ΑΜΚΑ: συμπληρωματική ταυτοποίηση."),
    ("Επικοινωνία", "Κινητό, email, διεύθυνση, πόλη και πρόσθετα τηλέφωνα/email: επικοινωνία με τον πελάτη."),
    ("Οδήγηση", "Ένδειξη οδηγού, δίπλωμα, κατηγορία, αριθμός και ημερομηνίες: στοιχεία για ασφάλιση οχήματος."),
    ("Λοιπά", "ΔΟΥ, νομική μορφή, επαγγελματικά στοιχεία, πηγή/σύσταση, συνεργάτης, έκπτωση και σημειώσεις."),
    ("Συμβόλαια / Οχήματα", "Συνδεδεμένες λίστες με αναζήτηση, φίλτρα, προσθήκη και άνοιγμα της αντίστοιχης καρτέλας."),
    ("Οικονομικά", "Παλαιό υπόλοιπο, εισπράξεις, πληρωμές και τρέχον υπόλοιπο. Κάθε κίνηση μπορεί να συνδεθεί με ασφαλιστήριο."),
    ("Ιστορικό ενεργειών", "Αυτόματη καταγραφή ημερομηνίας, ενέργειας, περιγραφής και χρήστη για την καρτέλα."),
], [3.6, 13.6])

doc.add_heading("5. Βάση δεδομένων, αντίγραφα και αναβαθμίσεις", level=1)
doc.add_paragraph(
    "Η βάση αποθηκεύεται στο %LOCALAPPDATA%\\AllInsurance\\Data\\allinsurance.db. "
    "Τα αντίγραφα ασφαλείας διαχειρίζονται από το μενού «Εργαλεία». "
    "Η επιλογή «Αναβάθμιση εφαρμογής» ελέγχει τα δημοσιευμένα GitHub Releases, "
    "κατεβάζει και επαληθεύει το νέο πακέτο, εγκαθιστά μόνο τα αρχεία της εφαρμογής "
    "σε ξεχωριστό φάκελο και επανεκκινεί. Η τοπική βάση και τα στοιχεία του χρήστη "
    "δεν αντικαθίστανται από την αναβάθμιση."
)

footer = section.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.add_run("KALYPSIS SOFTWARE SOLUTIONS  •  AllInsurance Backoffice Software")
for run in footer.runs:
    run.font.size = Pt(8)
    run.font.color.rgb = GRAY

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
doc.save(OUTPUT)
print(OUTPUT)
