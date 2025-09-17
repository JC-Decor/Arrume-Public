(function($){
  $(function(){
    const $btn = $('#btnSubmit'),
          $c1 = $('#AceiteContatoWhatsapp'),
          $c2 = $('#AceiteCompartilhamento'),
          $c3 = $('#AceiteUso');

    function refreshConsents() {
      const ok = $c1.is(':checked') && $c2.is(':checked') && $c3.is(':checked');
      $btn.prop('disabled', !ok || $btn.data('busy') === true);
    }
    $c1.add($c2).add($c3).on('change', refreshConsents);
    refreshConsents();

    $('#Telefone').on('input', function(){
      $(this).val(($(this).val()||'').replace(/\D/g,'').slice(0,13));
    });

    let lastCep = '', timer = null;

    function setBusy(b){
      $btn.data('busy', !!b);
      refreshConsents();
    }

    function fillFromViaCep(cep){
      if (!cep || cep.length !== 8 || cep === lastCep) return;
      lastCep = cep;
      setBusy(true);

      $.ajax({
        url: 'https://viacep.com.br/ws/'+encodeURIComponent(cep)+'/json/',
        dataType: 'json',
        timeout: 7000
      }).done(function(d){
        if(!d || d.erro){
          $('#Cidade').val('');
          $('#Uf').val('');
          $('#Bairro').val('');
          $('#Logradouro').val('');
          return;
        }
        $('#Cidade').val(d.localidade||'');
        $('#Uf').val(d.uf||'');
        $('#Bairro').val(d.bairro||'');
        $('#Logradouro').val((d.logradouro||'')+(d.complemento?' '+d.complemento:''));
      }).always(function(){
        setBusy(false);
      });
    }

    $('#Cep').on('input', function(){
      const cep = ($(this).val()||'').replace(/\D/g,'').slice(0,8);
      $(this).val(cep);
      clearTimeout(timer);
      if (cep.length === 8) {
        timer = setTimeout(function(){ fillFromViaCep(cep); }, 250);
      }
    });

    $('#Cep').on('blur', function(){
      const cep = ($(this).val()||'').replace(/\D/g,'').slice(0,8);
      $(this).val(cep);
      if (cep.length === 8) fillFromViaCep(cep);
    });

    $('#formLead').on('submit', function(e){
      if ($btn.data('busy') === true) {
        e.preventDefault();
        return false;
      }
      $('#Telefone').val(($('#Telefone').val()||'').replace(/\D/g,''));
      $('#Cep').val(($('#Cep').val()||'').replace(/\D/g,''));
    });
  });
})(jQuery);
