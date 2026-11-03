using AllInsurance.Desktop.Controls;
using AllInsurance.Domain.Entities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
using System.Globalization;
using System.IO;

namespace AllInsurance.Desktop.Views;

public sealed partial class BusinessRecordCardWindow
{
    private CheckBox VehicleCheck(StackPanel panel, string key, string label, bool value)
    {
        var field = new CheckBox { IsChecked = value, Height = 28, MinHeight = 28 };
        panel.Children.Add(CardFieldLayout.CreateRow(label, field));
        _vehicleChecks[key] = field;
        return field;
    }

    private bool VehicleChecked(string key) => _vehicleChecks[key].IsChecked == true;

    private void EnsurePrimaryOwner(Guid selectedId)
    {
        var selected = _vehiclePeople.FirstOrDefault(person => person.CustomerId == selectedId);
        if (selected is null)
        {
            var previous = _vehiclePeople.FirstOrDefault(person => person.IsPrimaryOwner);
            var previousShare = previous?.OwnershipPercent ?? 100;
            if (previous is not null)
            {
                previous.IsPrimaryOwner = false;
                previous.OwnershipPercent = 0;
            }
            selected = new VehiclePerson { CustomerId = selectedId, Role = "Ιδιοκτήτης",
                OwnershipPercent = previousShare > 0 ? previousShare : 100,
                IsPrimaryOwner = true };
            _vehiclePeople.Add(selected);
        }
        else
        {
            foreach (var person in _vehiclePeople) person.IsPrimaryOwner = person == selected;
            if (selected.Role == "Οδηγός") selected.Role = "Ιδιοκτήτης και οδηγός";
        }
        RefreshPeopleList();
    }

    private void ValidateVehiclePeople()
    {
        if (_vehiclePeople.Count(person => person.IsPrimaryOwner) != 1)
            throw new ArgumentException("Ορίστε έναν κύριο ιδιοκτήτη.");
        if (_vehiclePeople.Any(person => person.OwnershipPercent < 0 || person.OwnershipPercent > 100))
            throw new ArgumentException("Τα ποσοστά ιδιοκτησίας πρέπει να είναι από 0 έως 100.");
        if (_vehiclePeople.Where(person => person.Role != "Οδηγός")
                .Sum(person => person.OwnershipPercent) > 100)
            throw new ArgumentException("Το άθροισμα των ποσοστών ιδιοκτησίας δεν μπορεί να ξεπερνά το 100%.");
        if (_vehiclePeople.Any(person => person.Role == "Οδηγός" && person.OwnershipPercent != 0))
            throw new ArgumentException("Ο οδηγός χωρίς ιδιοκτησία πρέπει να έχει ποσοστό 0%.");
    }

    private void AddKteoImageRow(StackPanel panel, string? savedPath)
    {
        _kteoImagePath = savedPath;
        var content = new StackPanel { Spacing = 4 };
        var fileName = new TextBlock { Text = string.IsNullOrWhiteSpace(savedPath) ?
            "Δεν έχει επισυναφθεί εικόνα." : Path.GetFileName(savedPath),
            TextTrimming = TextTrimming.CharacterEllipsis };
        var actions = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Left };
        var attach = new ActionButton { Kind = "Upload", Label = "Επισύναψη" };
        attach.Click += async (_, _) =>
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
                    picker.FileTypeFilter.Add(extension);
                var selected = await picker.PickSingleFileAsync();
                if (selected is null) return;
                var directory = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "AllInsurance", "VehicleFiles");
                Directory.CreateDirectory(directory);
                var destination = Path.Combine(directory, Guid.NewGuid().ToString("N") +
                    Path.GetExtension(selected.Name).ToLowerInvariant());
                File.Copy(selected.Path, destination);
                _kteoImagePath = destination;
                fileName.Text = selected.Name;
            }
            catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
        };
        var view = new ActionButton { Kind = "Open", Label = "Προβολή" };
        view.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_kteoImagePath) || !File.Exists(_kteoImagePath))
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Η εικόνα ΚΤΕΟ δεν βρέθηκε."); return; }
            var image = await StorageFile.GetFileFromPathAsync(_kteoImagePath);
            await Windows.System.Launcher.LaunchFileAsync(image);
        };
        actions.Children.Add(attach); actions.Children.Add(view);
        content.Children.Add(fileName); content.Children.Add(actions);
        panel.Children.Add(CardFieldLayout.CreateRow("Συνημμένο ΚΤΕΟ", content));
    }

    private void BuildPeopleTab()
    {
        var panel = VehiclePanel();
        panel.Children.Add(new TextBlock { Text = "Κύριος ιδιοκτήτης, συνιδιοκτήτες και οδηγοί",
            FontSize = 16, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Μετά από αλλαγές στα πρόσωπα, πατήστε «Αποθήκευση» στην καρτέλα οχήματος.",
            FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη προσώπου" };
        add.Click += async (_, _) => await EditPersonAsync(null);
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία" };
        edit.Click += async (_, _) => await EditPersonAsync(_peopleList?.SelectedIndex);
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή" };
        delete.Click += async (_, _) =>
        {
            if (_peopleList?.SelectedIndex is not int index || index < 0 || index >= _vehiclePeople.Count)
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρόσωπο."); return; }
            if (_vehiclePeople[index].IsPrimaryOwner)
            { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ορίστε πρώτα άλλον κύριο ιδιοκτήτη."); return; }
            _vehiclePeople.RemoveAt(index);
            RefreshPeopleList();
        };
        actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete);
        panel.Children.Add(actions);
        _peopleList = VehicleTable(panel, ["Πρόσωπο", "Ρόλος", "Ποσοστό", "Κύριος"],
            [330, 280, 180, 150]);
        _peopleList.DoubleTapped += async (_, _) => await EditPersonAsync(_peopleList.SelectedIndex);
        RefreshPeopleList();
        AddTab("Πρόσωπα", panel);
    }

    private async Task EditPersonAsync(int? index)
    {
        var existing = index is >= 0 && index < _vehiclePeople.Count ? _vehiclePeople[index.Value] : null;
        if (index is not null && existing is null)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρόσωπο."); return; }
        var choices = _vehicleCustomers.Select(item => new Choice(item.Id, item.FullName)).ToArray();
        var person = new ComboBox { Header = "Πρόσωπο *", ItemsSource = choices,
            DisplayMemberPath = nameof(Choice.Name), HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = choices.FirstOrDefault(item => item.Id == existing?.CustomerId) };
        var role = new ComboBox { Header = "Ρόλος *", ItemsSource = new[]
            { "Ιδιοκτήτης", "Συνιδιοκτήτης", "Οδηγός", "Ιδιοκτήτης και οδηγός" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = existing?.Role ?? "Συνιδιοκτήτης" };
        var share = new TextBox { Header = "Ποσοστό ιδιοκτησίας (%) *",
            Text = existing?.OwnershipPercent.ToString("N2") ?? "0" };
        var primary = new CheckBox { Content = "Κύριος ιδιοκτήτης",
            IsChecked = existing?.IsPrimaryOwner ?? _vehiclePeople.All(item => !item.IsPrimaryOwner) };
        var form = new StackPanel { Spacing = 8, Width = 420 };
        form.Children.Add(person); form.Children.Add(role); form.Children.Add(share); form.Children.Add(primary);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = existing is null ? "Προσθήκη προσώπου στο όχημα" : "Επεξεργασία προσώπου",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        if (person.SelectedItem is not Choice { Id: Guid customerId } || role.SelectedItem is not string selectedRole)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε πρόσωπο και ρόλο."); return; }
        if (_vehiclePeople.Where((_, position) => position != index).Any(item => item.CustomerId == customerId))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Το πρόσωπο υπάρχει ήδη στο όχημα."); return; }
        if (!decimal.TryParse(share.Text, NumberStyles.Number,
                CultureInfo.GetCultureInfo("el-GR"), out var percentage) || percentage < 0 || percentage > 100)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Το ποσοστό πρέπει να είναι από 0 έως 100."); return; }
        if (selectedRole == "Οδηγός" && (percentage != 0 || primary.IsChecked == true))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ο οδηγός χωρίς ιδιοκτησία έχει ποσοστό 0% και δεν είναι κύριος ιδιοκτήτης."); return; }
        var updated = new VehiclePerson { CustomerId = customerId, Role = selectedRole,
            OwnershipPercent = percentage, IsPrimaryOwner = primary.IsChecked == true };
        if (updated.IsPrimaryOwner)
        {
            foreach (var item in _vehiclePeople) item.IsPrimaryOwner = false;
            _customerChoice!.SelectedItem = (_customerChoice.ItemsSource as IEnumerable<Choice>)?
                .FirstOrDefault(item => item.Id == customerId);
        }
        if (existing is null) _vehiclePeople.Add(updated);
        else _vehiclePeople[index!.Value] = updated;
        RefreshPeopleList();
    }

    private void RefreshPeopleList()
    {
        if (_peopleList is null) return;
        FillVehicleTable(_peopleList, _vehiclePeople.Select(person => new[]
        {
            _vehicleCustomers.FirstOrDefault(item => item.Id == person.CustomerId)?.FullName ?? "Άγνωστος πελάτης",
            person.Role, $"{person.OwnershipPercent:N2} %", person.IsPrimaryOwner ? "Ναι" : "Όχι"
        }).ToArray(), [330, 280, 180, 150]);
    }

    private void BuildVehiclePoliciesTab()
    {
        var panel = VehiclePanel();
        panel.Children.Add(new TextBlock { Text = "Αιτήσεις, ειδοποιητήρια και ασφαλιστήρια οχήματος",
            FontSize = 16 });
        var add = new ActionButton { Kind = "Add", Label = "Νέα αίτηση ασφάλισης",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = _record.Id != Guid.Empty };
        _newVehiclePolicyButton = add;
        add.Click += async (_, _) => await OpenNewPolicyForVehicleAsync();
        panel.Children.Add(add);
        _policiesList = VehicleTable(panel,
            ["Αριθμός / άνοιγμα", "Τύπος", "Κατάσταση", "Ημερομηνία", "Πελάτης"],
            [250, 210, 190, 170, 200]);
        FillVehicleTable(_policiesList, _vehiclePolicies.Select(item => new[]
        {
            item.Title, item.DocumentType ?? "Ασφαλιστήριο", item.Status ?? "—",
            item.EventDate?.ToString("dd/MM/yyyy") ?? "—",
            _vehicleCustomers.FirstOrDefault(customer => customer.Id == item.CustomerId)?.FullName ?? "—"
        }).ToArray(), [250, 210, 190, 170, 200], _vehiclePolicies.Select(item => item.Id).ToArray(),
            async id => await OpenLinkedPolicyAsync(id));
        AddTab("Συμβόλαια", panel);
    }

    private async Task OpenLinkedPolicyAsync(Guid id)
    {
        if (_linkedPolicyCard is not null) { _linkedPolicyCard.BringToFront(); return; }
        _linkedPolicyCard = new BusinessRecordCardWindow("policies", recordId: id,
            ownerHandle: WindowNative.GetWindowHandle(this));
        _linkedPolicyCard.RecordSaved += async (_, _) => await RefreshVehiclePoliciesAsync();
        _linkedPolicyCard.Closed += (_, _) => { _linkedPolicyCard = null; BringToFront(); };
        _linkedPolicyCard.BringToFront();
    }

    private async Task OpenNewPolicyForVehicleAsync()
    {
        if (_record.Id == Guid.Empty || _record.CustomerId is not Guid ownerId)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα το όχημα και τον κύριο ιδιοκτήτη."); return; }
        if (_linkedPolicyCard is not null) { _linkedPolicyCard.BringToFront(); return; }
        _linkedPolicyCard = new BusinessRecordCardWindow("policies", customerId: ownerId,
            ownerHandle: WindowNative.GetWindowHandle(this), vehicleId: _record.Id);
        _linkedPolicyCard.RecordSaved += async (_, _) =>
        {
            await RefreshVehiclePoliciesAsync();
            RecordSaved?.Invoke(this, EventArgs.Empty);
        };
        _linkedPolicyCard.Closed += (_, _) => { _linkedPolicyCard = null; BringToFront(); };
        _linkedPolicyCard.BringToFront();
    }

    private async Task RefreshVehiclePoliciesAsync()
    {
        _vehiclePolicies = (await _records.SearchAsync("policies"))
            .Where(item => BusinessRecordDetails.Read<PolicyCardData>(item.DetailsJson).VehicleId == _record.Id)
            .ToArray();
        if (_policiesList is not null)
            FillVehicleTable(_policiesList, _vehiclePolicies.Select(item => new[]
            {
                item.Title, item.DocumentType ?? "Ασφαλιστήριο", item.Status ?? "—",
                item.EventDate?.ToString("dd/MM/yyyy") ?? "—",
                _vehicleCustomers.FirstOrDefault(customer => customer.Id == item.CustomerId)?.FullName ?? "—"
            }).ToArray(), [250, 210, 190, 170, 200], _vehiclePolicies.Select(item => item.Id).ToArray(),
                async id => await OpenLinkedPolicyAsync(id));
    }

    private void BuildVehicleClaimsTab()
    {
        var panel = VehiclePanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var add = new ActionButton { Kind = "Add", Label = "Προσθήκη ζημιάς",
            IsEnabled = _record.Id != Guid.Empty };
        _addVehicleClaimButton = add;
        add.Click += async (_, _) => await EditClaimAsync(null);
        var edit = new ActionButton { Kind = "Edit", Label = "Επεξεργασία ζημιάς" };
        edit.Click += async (_, _) =>
        {
            if (SelectedClaimId() is Guid id) await EditClaimAsync(id);
            else await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε ζημιά.");
        };
        var delete = new ActionButton { Kind = "Delete", Label = "Διαγραφή ζημιάς" };
        delete.Click += async (_, _) => await DeleteClaimAsync(SelectedClaimId());
        actions.Children.Add(add); actions.Children.Add(edit); actions.Children.Add(delete);
        panel.Children.Add(actions);
        _claimsList = VehicleTable(panel,
            ["Ζημιά / άνοιγμα", "Κατάσταση", "Ημερομηνία", "Πελάτης", "Σημειώσεις"],
            [240, 180, 170, 200, 250]);
        RefreshClaimsList();
        AddTab("Ζημιές", panel);
    }

    private Guid? SelectedClaimId() => (_claimsList?.SelectedItem as ListViewItem)?.Tag is Guid id ? id : null;

    private async Task EditClaimAsync(Guid? id)
    {
        if (_record.Id == Guid.Empty)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Αποθηκεύστε πρώτα το όχημα."); return; }
        var record = id is Guid recordId ? await _records.GetAsync(recordId) :
            new BusinessRecord { SectionKey = "claims", Title = "", CustomerId = _record.CustomerId,
                DetailsJson = BusinessRecordDetails.Write(new ClaimCardData { VehicleId = _record.Id }) };
        if (record is null || record.SectionKey != "claims" ||
            BusinessRecordDetails.Read<ClaimCardData>(record.DetailsJson).VehicleId != _record.Id)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Η ζημιά δεν ανήκει σε αυτό το όχημα."); return; }
        var reference = new TextBox { Header = "Αριθμός ζημιάς *", Text = record.Reference ?? record.Title };
        var status = new ComboBox { Header = "Κατάσταση", ItemsSource = new[]
            { "Νέα", "Σε εξέλιξη", "Ολοκληρωμένη", "Κλειστή" },
            SelectedItem = record.Status ?? "Νέα", HorizontalAlignment = HorizontalAlignment.Stretch };
        var date = new CalendarDatePicker { Header = "Ημερομηνία συμβάντος",
            Date = record.EventDate is DateTime eventDate ? new DateTimeOffset(eventDate) : null };
        var notes = new TextBox { Header = "Σημειώσεις", Text = record.Notes ?? "",
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100 };
        var form = new StackPanel { Spacing = 8, Width = 430 };
        form.Children.Add(reference); form.Children.Add(status); form.Children.Add(date); form.Children.Add(notes);
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = id is null ? "Προσθήκη ζημιάς" : "Επεξεργασία ζημιάς",
            Content = form, PrimaryButtonText = "Αποθήκευση", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(reference.Text))
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Ο αριθμός ζημιάς είναι υποχρεωτικός."); return; }
        try
        {
            record.Title = reference.Text.Trim(); record.Reference = record.Title;
            record.Status = status.SelectedItem as string;
            record.EventDate = date.Date?.Date;
            record.Notes = notes.Text.Trim();
            record.CustomerId = _record.CustomerId;
            record.DetailsJson = BusinessRecordDetails.Write(new ClaimCardData { VehicleId = _record.Id });
            await _records.SaveAsync(record);
            await RefreshVehicleClaimsAsync();
            RecordSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task DeleteClaimAsync(Guid? id)
    {
        if (id is null)
        { await UiAlerts.ShowErrorAsync(_root.XamlRoot, "Επιλέξτε ζημιά."); return; }
        var dialog = new ContentDialog { XamlRoot = _root.XamlRoot,
            Title = "Διαγραφή ζημιάς", Content = "Να διαγραφεί η επιλεγμένη ζημιά;",
            PrimaryButtonText = "Διαγραφή", CloseButtonText = "Ακύρωση" };
        if (await DialogButtonStyles.ShowAsync(dialog) != ContentDialogResult.Primary) return;
        try { await _records.DeleteAsync(id.Value); await RefreshVehicleClaimsAsync();
            RecordSaved?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception) { await UiAlerts.ShowErrorAsync(_root.XamlRoot, exception.Message); }
    }

    private async Task RefreshVehicleClaimsAsync()
    {
        _vehicleClaims = (await _records.SearchAsync("claims"))
            .Where(item => BusinessRecordDetails.Read<ClaimCardData>(item.DetailsJson).VehicleId == _record.Id)
            .ToArray();
        RefreshClaimsList();
    }

    private void RefreshClaimsList()
    {
        if (_claimsList is null) return;
        FillVehicleTable(_claimsList, _vehicleClaims.Select(item => new[]
        {
            item.Title, item.Status ?? "—", item.EventDate?.ToString("dd/MM/yyyy") ?? "—",
            _vehicleCustomers.FirstOrDefault(customer => customer.Id == item.CustomerId)?.FullName ?? "—",
            item.Notes ?? "—"
        }).ToArray(), [240, 180, 170, 200, 250], _vehicleClaims.Select(item => item.Id).ToArray(),
            async id => await EditClaimAsync(id));
    }

    private static StackPanel VehiclePanel() => new() { Spacing = 9,
        Margin = new Thickness(22, 12, 22, 20) };

    private ListView VehicleTable(StackPanel panel, string[] captions, int[] widths)
    {
        var total = widths.Sum();
        var header = new Grid { Width = total,
            Background = new SolidColorBrush(IsDarkTheme() ? Color.FromArgb(255, 55, 65, 77) :
                Color.FromArgb(255, 224, 233, 241)) };
        foreach (var width in widths) header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        for (var index = 0; index < captions.Length; index++)
        {
            var cell = new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                BorderThickness = index == captions.Length - 1 ? new Thickness(0) :
                    new Thickness(0, 0, 1, 0), Padding = new Thickness(10, 8, 10, 8),
                Child = new TextBlock { Text = captions[index],
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } };
            Grid.SetColumn(cell, index); header.Children.Add(cell);
        }
        panel.Children.Add(new Border { Child = header, Width = total + 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1) });
        var list = new ListView { Width = total + 2, Height = 420,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
            BorderThickness = new Thickness(1, 0, 1, 1) };
        panel.Children.Add(list);
        return list;
    }

    private void FillVehicleTable(ListView list, string[][] rows, int[] widths,
        Guid[]? ids = null, Func<Guid, Task>? open = null)
    {
        list.Items.Clear();
        var total = widths.Sum();
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var id = ids is null ? (Guid?)null : ids[rowIndex];
            var grid = new Grid { Width = total, Height = 42 };
            foreach (var width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
            for (var column = 0; column < rows[rowIndex].Length; column++)
            {
                UIElement content;
                if (column == 0 && id is Guid linkedId && open is not null)
                {
                    var link = new HyperlinkButton { Content = rows[rowIndex][column],
                        Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
                    link.Click += async (_, _) => await open(linkedId);
                    content = link;
                }
                else content = new TextBlock { Text = rows[rowIndex][column],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center };
                var border = new Border { Child = content,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 130, 149, 168)),
                    BorderThickness = column == rows[rowIndex].Length - 1 ? new Thickness(0, 0, 0, 1) :
                        new Thickness(0, 0, 1, 1), Padding = new Thickness(9, 3, 9, 3) };
                Grid.SetColumn(border, column); grid.Children.Add(border);
            }
            list.Items.Add(new ListViewItem { Content = grid, Tag = id,
                Width = total, Height = 42, Padding = new Thickness(0), Margin = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
    }
}
