// Ask before doing anything destructive: <form data-confirm="Are you sure?">
document.addEventListener('submit', function (event) {
    var message = event.target.getAttribute('data-confirm');
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// Registration: only show the organiser sign-up code when "Organiser" is chosen
document.addEventListener('DOMContentLoaded', function () {
    var roleInputs = document.querySelectorAll('input[name="Role"]');
    var codeBox = document.getElementById('organiser-code-box');
    if (!roleInputs.length || !codeBox) return;

    function update() {
        var chosen = document.querySelector('input[name="Role"]:checked');
        codeBox.style.display = chosen && chosen.value === 'Organiser' ? 'block' : 'none';
    }
    roleInputs.forEach(function (input) { input.addEventListener('change', update); });
    update();
});
